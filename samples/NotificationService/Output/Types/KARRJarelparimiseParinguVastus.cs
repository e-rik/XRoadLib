using Optional;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XRoadLib.Serialization;

namespace MyNamespace
{
    public class KARRJarelparimiseParinguVastus : IXRoadXmlSerializable
    {
        public Option<Isik> KaristatudIsik { get; set; }

        public class ParingudType : IXRoadXmlSerializable
        {
            public IList<KARRParing> item { get; set; }

            void IXRoadXmlSerializable.ReadXml(XmlReader reader, XRoadMessage message)
            {
            }

            void IXRoadXmlSerializable.WriteXml(XmlWriter writer, XRoadMessage message)
            {
            }
        }

        public Option<ParingudType> Paringud { get; set; }
        public Option<DateTime> ParinguTegemiseAeg { get; set; }
        public Option<DateTime?> ParinguTegemiseValitudAeg { get; set; }
        public Option<long> PdfFailiObjektID { get; set; }

        void IXRoadXmlSerializable.ReadXml(XmlReader reader, XRoadMessage message)
        {
        }

        void IXRoadXmlSerializable.WriteXml(XmlWriter writer, XRoadMessage message)
        {
        }
    }
}