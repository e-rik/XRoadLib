using System.Xml;

namespace XRoadLib.Wsdl
{
    public class Soap12BodyBinding : ServiceDescriptionFormatExtension
    {
        public string Encoding { get; set; }
        public string Namespace { get; set; }
        public SoapBindingUse Use { get; set; } = SoapBindingUse.Default;

        internal override void Write(XmlWriter writer)
        {
            writer.WriteStartElement(PrefixConstants.Soap12, "body", NamespaceConstants.Soap12);

            if (!string.IsNullOrEmpty(Encoding))
                writer.WriteAttributeString("encodingStyle", Encoding);

            if (!string.IsNullOrEmpty(Namespace))
                writer.WriteAttributeString("namespace", Namespace);

            if (Use != SoapBindingUse.Default)
                writer.WriteAttributeString("use", Use == SoapBindingUse.Encoded ? "encoded" : "literal");

            writer.WriteEndElement();
        }
    }
}