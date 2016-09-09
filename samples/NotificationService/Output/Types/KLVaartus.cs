using Optional;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XRoadLib.Serialization;

namespace MyNamespace
{
    public class KLVaartus : IXRoadXmlSerializable
    {
        public Option<DateTime?> AlgusKP { get; set; }
        public Option<string> Kirjeldus { get; set; }
        public Option<DateTime?> LoppKP { get; set; }
        public Option<string> LyhiVaartus { get; set; }
        public Option<long> ObjektID { get; set; }

        public class SeaduseSattedType : IXRoadXmlSerializable
        {
            public IList<SeaduseSate> item { get; set; }

            void IXRoadXmlSerializable.ReadXml(XmlReader reader, XRoadMessage message)
            {
            }

            void IXRoadXmlSerializable.WriteXml(XmlWriter writer, XRoadMessage message)
            {
            }
        }

        public Option<SeaduseSattedType> SeaduseSatted { get; set; }
        public Option<string> Tunnus { get; set; }
        public Option<string> Vaartus { get; set; }

        void IXRoadXmlSerializable.ReadXml(XmlReader reader, XRoadMessage message)
        {
        }

        void IXRoadXmlSerializable.WriteXml(XmlWriter writer, XRoadMessage message)
        {
        }
    }
}