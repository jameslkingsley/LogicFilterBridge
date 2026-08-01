using System;
using System.Xml.Serialization;
using Assets.Scripts.Objects.Electrical;

namespace LogicFilterBridge;

[Serializable]
public sealed class LogicFilterBridgeSaveData : LogicBaseSaveData
{
    [XmlElement]
    public int FilterMode;
}
