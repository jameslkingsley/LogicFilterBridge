using System;
using System.Collections.Generic;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects.Pipes;

namespace LogicFilterBridge;

internal static class BridgeNetworkManager
{
    private static readonly object RegistryLock = new object();
    private static readonly List<LogicFilterBridge> Bridges = new List<LogicFilterBridge>();

    [ThreadStatic]
    private static HashSet<CableNetwork> _refreshTraversal;

    [ThreadStatic]
    private static HashSet<CableNetwork> _dirtyTraversal;

    internal static void Register(LogicFilterBridge bridge)
    {
        if (bridge == null)
        {
            return;
        }

        lock (RegistryLock)
        {
            if (!Bridges.Contains(bridge))
            {
                Bridges.Add(bridge);
            }
        }
    }

    internal static void Unregister(LogicFilterBridge bridge)
    {
        if (ReferenceEquals(bridge, null))
        {
            return;
        }

        lock (RegistryLock)
        {
            Bridges.Remove(bridge);
        }
    }

    internal static void PopulateOutputNetwork(CableNetwork network)
    {
        if (network == null)
        {
            return;
        }

        List<LogicFilterBridge> bridges = Snapshot();
        bool hasOutputBridge = false;
        for (int index = 0; index < bridges.Count; index++)
        {
            LogicFilterBridge bridge = bridges[index];
            if (bridge != null && bridge.OutputNetwork1 == network)
            {
                hasOutputBridge = true;
                break;
            }
        }

        // This patch runs for every cable network in the world. Networks with no
        // bridge output must remain byte-for-byte vanilla behavior.
        if (!hasOutputBridge)
        {
            return;
        }

        _refreshTraversal ??= new HashSet<CableNetwork>();
        if (!_refreshTraversal.Add(network))
        {
            return;
        }

        try
        {
            List<Device> outputDevices = network.DataDeviceList;

            // A bridge is a configuration target only on its input side. Retain it on
            // invalid same-network wiring so the player can still correct that wiring.
            for (int index = bridges.Count - 1; index >= 0; index--)
            {
                LogicFilterBridge bridge = bridges[index];
                if (bridge != null && bridge.OutputNetwork1 == network && bridge.InputNetwork1 != network)
                {
                    RemoveByReference(outputDevices, bridge.ReferenceId);
                }
            }

            HashSet<long> referenceIds = new HashSet<long>();
            HashSet<Device> zeroReferenceDevices = new HashSet<Device>();
            for (int index = outputDevices.Count - 1; index >= 0; index--)
            {
                Device device = outputDevices[index];
                if (device == null)
                {
                    continue;
                }

                if (device.ReferenceId == 0)
                {
                    zeroReferenceDevices.Add(device);
                }
                else
                {
                    referenceIds.Add(device.ReferenceId);
                }
            }

            for (int bridgeIndex = 0; bridgeIndex < bridges.Count; bridgeIndex++)
            {
                LogicFilterBridge bridge = bridges[bridgeIndex];
                if (bridge == null || bridge.OutputNetwork1 != network)
                {
                    continue;
                }

                if (!bridge.CanForwardDevices)
                {
                    continue;
                }

                CableNetwork inputNetwork = bridge.InputNetwork1;
                if (inputNetwork == null || _refreshTraversal.Contains(inputNetwork))
                {
                    continue;
                }

                List<Device> inputDevices = inputNetwork.DataDeviceList;
                for (int deviceIndex = 0; deviceIndex < inputDevices.Count; deviceIndex++)
                {
                    Device candidate = inputDevices[deviceIndex];
                    if (!bridge.Accepts(candidate))
                    {
                        continue;
                    }

                    bool isNew = candidate.ReferenceId == 0
                        ? zeroReferenceDevices.Add(candidate)
                        : referenceIds.Add(candidate.ReferenceId);
                    if (isNew)
                    {
                        outputDevices.Add(candidate);
                    }
                }
            }

            // An imported path can loop back to a bridge that is physically visible on
            // another input network. Enforce the output-side hiding rule after all paths
            // have been merged as well as before traversal.
            for (int index = bridges.Count - 1; index >= 0; index--)
            {
                LogicFilterBridge bridge = bridges[index];
                if (bridge != null && bridge.OutputNetwork1 == network && bridge.InputNetwork1 != network)
                {
                    RemoveByReference(outputDevices, bridge.ReferenceId);
                }
            }


            // Logic units (including IC Housings) cache a separately sorted device list.
            // The cable list has now changed without a physical cable event, so invalidate
            // those consumer caches. Skip bridges for which this is only their output side;
            // notifying those would immediately dirty the network currently being rebuilt.
            Device refreshSource = null;
            for (int index = 0; index < bridges.Count; index++)
            {
                LogicFilterBridge bridge = bridges[index];
                if (bridge != null && bridge.OutputNetwork1 == network)
                {
                    refreshSource = bridge;
                    break;
                }
            }

            if (refreshSource != null)
            {
                for (int index = network.DeviceList.Count - 1; index >= 0; index--)
                {
                    Device localDevice = network.DeviceList[index];
                    if (localDevice == null)
                    {
                        continue;
                    }

                    if (localDevice is LogicFilterBridge localBridge && localBridge.OutputNetwork1 == network)
                    {
                        continue;
                    }

                    localDevice.OnNetworkedRefresh(refreshSource);
                }
            }
        }
        finally
        {
            _refreshTraversal.Remove(network);
        }
    }

    internal static void PropagateDirty(CableNetwork sourceNetwork)
    {
        if (sourceNetwork == null)
        {
            return;
        }

        _dirtyTraversal ??= new HashSet<CableNetwork>();
        if (!_dirtyTraversal.Add(sourceNetwork))
        {
            return;
        }

        try
        {
            List<LogicFilterBridge> bridges = Snapshot();
            for (int index = 0; index < bridges.Count; index++)
            {
                LogicFilterBridge bridge = bridges[index];
                if (bridge != null && bridge.InputNetwork1 == sourceNetwork)
                {
                    Dirty(bridge.OutputNetwork1);
                }
            }
        }
        finally
        {
            _dirtyTraversal.Remove(sourceNetwork);
        }
    }

    internal static void HandleDataOnlyDirty(CableNetwork network)
    {
        if (network == null)
        {
            return;
        }

        List<LogicFilterBridge> bridges = Snapshot();
        for (int index = 0; index < bridges.Count; index++)
        {
            LogicFilterBridge bridge = bridges[index];
            if (bridge != null && (bridge.InputNetwork1 == network || bridge.OutputNetwork1 == network))
            {
                // The installed build refreshes DataDeviceList from the combined dirty
                // state. Promote only bridge-related networks, never unrelated IC grids.
                network.DirtyPowerAndDataDeviceLists();
                return;
            }
        }
    }

    internal static void InvalidateAllNameFilters()
    {
        List<LogicFilterBridge> bridges = Snapshot();
        for (int index = 0; index < bridges.Count; index++)
        {
            LogicFilterBridge bridge = bridges[index];
            if (bridge != null && bridge.Mode == (int)LogicFilterMode.Name)
            {
                Dirty(bridge.OutputNetwork1);
            }
        }
    }

    internal static void Dirty(CableNetwork network)
    {
        network?.DirtyPowerAndDataDeviceLists();
    }

    private static List<LogicFilterBridge> Snapshot()
    {
        lock (RegistryLock)
        {
            for (int index = Bridges.Count - 1; index >= 0; index--)
            {
                if (Bridges[index] == null)
                {
                    Bridges.RemoveAt(index);
                }
            }

            return new List<LogicFilterBridge>(Bridges);
        }
    }

    private static void RemoveByReference(List<Device> devices, long referenceId)
    {
        for (int index = devices.Count - 1; index >= 0; index--)
        {
            Device device = devices[index];
            if (device != null && device.ReferenceId == referenceId)
            {
                devices.RemoveAt(index);
            }
        }
    }
}
