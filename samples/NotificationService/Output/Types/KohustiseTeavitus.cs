using Optional;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XRoadLib.Serialization;

namespace MyNamespace
{
    public class KohustiseTeavitus : StandardTeavitus, IXRoadXmlSerializable
    {
        public Option<Kohustis> Kohustis { get; set; }
        public Option<long> KohustiseObjektID { get; set; }
        public Option<long> KohustiseVersID { get; set; }

        void IXRoadXmlSerializable.ReadXml(XmlReader reader, XRoadMessage message)
        {
        }

        void IXRoadXmlSerializable.WriteXml(XmlWriter writer, XRoadMessage message)
        {
        }
    }
}