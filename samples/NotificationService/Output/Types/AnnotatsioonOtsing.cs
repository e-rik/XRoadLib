using Optional;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XRoadLib.Serialization;

namespace MyNamespace
{
    public class AnnotatsioonOtsing : IXRoadXmlSerializable
    {
        public Option<IList<long>> MarksonadObjektID { get; set; }
        public Option<long?> ObjektID { get; set; }
        public Option<long?> SeisundKL { get; set; }
        public Option<string> Sisu { get; set; }

        void IXRoadXmlSerializable.ReadXml(XmlReader reader, XRoadMessage message)
        {
        }

        void IXRoadXmlSerializable.WriteXml(XmlWriter writer, XRoadMessage message)
        {
        }
    }
}