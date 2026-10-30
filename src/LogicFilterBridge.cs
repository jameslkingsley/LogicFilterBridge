using System;
using Assets.Scripts;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Networking;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Objects.Pipes;

namespace LogicFilterBridge;

public enum LogicFilterMode
{
    All = 0,
    Prefab = 1,
    Name = 2
}

/// <summary>
/// A one-way visibility bridge between the Logic Mirror's input and output data connectors.
/// The devices added to the output list remain their original live instances.
/// </summary>
public sealed class LogicFilterBridge : LogicInputBase
{
    private const ushort ModeNetworkUpdateFlag = 1024;
    private const double LargestPackedAscii6 = 281474976710655d;

    private int _filterMode;
    private string _nameFilter;
    private bool _nameFilterValid;
    private CableNetwork _previousInputNetwork;
    private CableNetwork _previousOutputNetwork;

    public override int Mode
    {
        get => _filterMode;
        set
        {
            int normalized = Math.Max((int)LogicFilterMode.All, Math.Min((int)LogicFilterMode.Name, value));
            if (_filterMode == normalized)
            {
                return;
            }

            _filterMode = normalized;
            RefreshNameFilter();
            if (NetworkManager.IsServer)
            {
                NetworkUpdateFlags |= ModeNetworkUpdateFlag;
            }

            InvalidateForwarding();
        }
    }

    public override double Setting
    {
        get => base.Setting;
        set
        {
            double previous = base.Setting;
            base.Setting = value;
            if (previous.Equals(value))
            {
                return;
            }

            // LogicUnitBase uses an approximate comparison. Packed strings and hashes are exact
            // integer payloads, so force its normal Setting bit for any exact value change.
            if (NetworkManager.IsServer)
            {
                NetworkUpdateFlags |= 256;
            }

            RefreshNameFilter();
            InvalidateForwarding();
        }
    }

    internal bool CanForwardDevices =>
        IsStructureCompleted &&
        OnOff &&
        Powered &&
        InputNetwork1 != null &&
        OutputNetwork1 != null &&
        InputNetwork1 != OutputNetwork1;

    internal bool Accepts(Device device)
    {
        if (device == null || ReferenceEquals(device, this))
        {
            return false;
        }

        switch ((LogicFilterMode)_filterMode)
        {
            case LogicFilterMode.All:
                return true;
            case LogicFilterMode.Prefab:
                return TryGetPrefabFilter(out int prefabHash) && device.PrefabHash == prefabHash;
            case LogicFilterMode.Name:
                return _nameFilterValid &&
                       !string.IsNullOrEmpty(device.DisplayName) &&
                       device.DisplayName.IndexOf(_nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
            default:
                return false;
        }
    }

    public override bool CanLogicRead(LogicType logicType)
    {
        if (logicType == LogicType.Mode || logicType == LogicType.Setting)
        {
            return IsStructureCompleted;
        }

        return base.CanLogicRead(logicType);
    }

    public override bool CanLogicWrite(LogicType logicType)
    {
        if (logicType == LogicType.Mode || logicType == LogicType.Setting)
        {
            return IsStructureCompleted;
        }

        return base.CanLogicWrite(logicType);
    }

    public override double GetLogicValue(LogicType logicType)
    {
        return logicType switch
        {
            LogicType.Mode => Mode,
            LogicType.Setting => Setting,
            _ => base.GetLogicValue(logicType)
        };
    }

    public override void SetLogicValue(LogicType logicType, double value)
    {
        if (logicType == LogicType.Mode)
        {
            if (!IsStructureCompleted || !GameManager.RunSimulation || double.IsNaN(value) || double.IsInfinity(value))
            {
                return;
            }

            double truncated = Math.Truncate(value);
            Mode = (int)Math.Max((int)LogicFilterMode.All, Math.Min((int)LogicFilterMode.Name, truncated));
            return;
        }

        if (logicType == LogicType.Setting)
        {
            if (IsStructureCompleted && GameManager.RunSimulation)
            {
                Setting = value;
            }
            return;
        }

        base.SetLogicValue(logicType, value);
    }

    public override void BuildUpdate(RocketBinaryWriter writer, ushort networkUpdateType)
    {
        base.BuildUpdate(writer, networkUpdateType);
        if (Thing.IsNetworkUpdateRequired(ModeNetworkUpdateFlag, networkUpdateType))
        {
            writer.WriteByte((byte)Mode);
        }
    }

    public override void ProcessUpdate(RocketBinaryReader reader, ushort networkUpdateType)
    {
        base.ProcessUpdate(reader, networkUpdateType);
        if (Thing.IsNetworkUpdateRequired(ModeNetworkUpdateFlag, networkUpdateType))
        {
            Mode = reader.ReadByte();
        }
    }

    public override void SerializeOnJoin(RocketBinaryWriter writer)
    {
        base.SerializeOnJoin(writer);
        writer.WriteByte((byte)Mode);
    }

    public override void DeserializeOnJoin(RocketBinaryReader reader)
    {
        base.DeserializeOnJoin(reader);
        Mode = reader.ReadByte();
    }

    public override ThingSaveData SerializeSave()
    {
        ThingSaveData savedData = new LogicFilterBridgeSaveData();
        InitialiseSaveData(ref savedData);
        return savedData;
    }

    public override void DeserializeSave(ThingSaveData savedData)
    {
        base.DeserializeSave(savedData);
        if (savedData is LogicFilterBridgeSaveData bridgeSaveData)
        {
            Mode = bridgeSaveData.FilterMode;
        }
    }

    protected override void InitialiseSaveData(ref ThingSaveData savedData)
    {
        base.InitialiseSaveData(ref savedData);
        if (savedData is LogicFilterBridgeSaveData bridgeSaveData)
        {
            bridgeSaveData.FilterMode = Mode;
        }
    }

    public override void OnRegistered(Cell cell)
    {
        base.OnRegistered(cell);
        BridgeNetworkManager.Register(this);
        RememberNetworks();
        InvalidateForwarding();
    }

    public override void OnDeregistered()
    {
        CableNetwork input = InputNetwork1;
        CableNetwork output = OutputNetwork1;
        BridgeNetworkManager.Unregister(this);
        base.OnDeregistered();
        BridgeNetworkManager.Dirty(input);
        BridgeNetworkManager.Dirty(output);
    }

    public override void OnDestroy()
    {
        BridgeNetworkManager.Unregister(this);
        base.OnDestroy();
    }

    public override void OnFinishedLoad()
    {
        base.OnFinishedLoad();
        BridgeNetworkManager.Register(this);
        RememberNetworks();
        RefreshNameFilter();
        InvalidateForwarding();
    }

    public override void OnNetworkChange()
    {
        base.OnNetworkChange();
        BridgeNetworkManager.Dirty(_previousInputNetwork);
        BridgeNetworkManager.Dirty(_previousOutputNetwork);
        RememberNetworks();
        InvalidateForwarding();
    }

    public override void OnNetworkedDeviceNameChanged(Device device)
    {
        base.OnNetworkedDeviceNameChanged(device);
        InvalidateForwarding();
    }

    public override void OnNetworkedRefresh(Device device)
    {
        // LogicUnitBase routes this callback through OnNetworkChange, which is useful
        // for its private sorted-device caches but would also re-dirty this input
        // network through our cabling invalidation. The bridge reads DataDeviceList
        // directly, so only its downstream output needs invalidating here.
        InvalidateForwarding();
    }

    public override void OnInteractableUpdated(Interactable interactable)
    {
        base.OnInteractableUpdated(interactable);
        InvalidateForwarding();
    }

    protected override void OnBuildStateUpdated(int newState, int previousState)
    {
        base.OnBuildStateUpdated(newState, previousState);
        InvalidateForwarding();
    }

    private bool TryGetPrefabFilter(out int prefabHash)
    {
        double value = Setting;
        if (double.IsNaN(value) || double.IsInfinity(value) || value < int.MinValue || value > int.MaxValue || Math.Truncate(value) != value)
        {
            prefabHash = 0;
            return false;
        }

        prefabHash = (int)value;
        return true;
    }

    private void RefreshNameFilter()
    {
        _nameFilter = null;
        _nameFilterValid = false;

        double packed = Setting;
        if (double.IsNaN(packed) || double.IsInfinity(packed) || packed <= 0d || packed > LargestPackedAscii6 || Math.Truncate(packed) != packed)
        {
            return;
        }

        string unpacked = ProgrammableChip.UnpackAscii6(packed, signed: false);
        if (string.IsNullOrEmpty(unpacked) || unpacked.Length > 6)
        {
            return;
        }

        for (int index = 0; index < unpacked.Length; index++)
        {
            char character = unpacked[index];
            if (character == '\0' || character > 0x7f)
            {
                return;
            }
        }

        _nameFilter = unpacked;
        _nameFilterValid = true;
    }

    private void RememberNetworks()
    {
        _previousInputNetwork = InputNetwork1;
        _previousOutputNetwork = OutputNetwork1;
    }

    private void InvalidateForwarding()
    {
        BridgeNetworkManager.Dirty(OutputNetwork1);
    }
}
