using Optional;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XRoadLib.Serialization;

namespace MyNamespace
{
    public class MenetluseAlustamineRequest : IXRoadXmlSerializable
    {
        public Menetlus Menetlus { get; set; }
        public IList<Syyteosyndmus> Syyteosyndmused { get; set; }
        public Osaline Menetleja { get; set; }

        void IXRoadXmlSerializable.ReadXml(XmlReader reader, XRoadMessage message)
        {
        }

        void IXRoadXmlSerializable.WriteXml(XmlWriter writer, XRoadMessage message)
        {
        }
    }
}