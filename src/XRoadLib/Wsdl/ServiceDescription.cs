using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace XRoadLib.Wsdl
{
    public class ServiceDescription : NamedItem
    {
        protected override string ElementName { get; } = "definitions";

        public List<Binding> Bindings { get; } = new List<Binding>();
        public List<Message> Messages { get; } = new List<Message>();
        public List<PortType> PortTypes { get; } = new List<PortType>();
        public List<Service> Services { get; } = new List<Service>();
        public string TargetNamespace { get; set; }
        public Types Types { get; } = new Types();

        protected override async Task WriteAttributesAsync(XmlWriter writer)
        {
            await base.WriteAttributesAsync(writer).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(TargetNamespace))
                await writer.WriteAttributeStringAsync(null, "targetNamespace", null, TargetNamespace).ConfigureAwait(false);
        }

        protected override async Task WriteElementsAsync(XmlWriter writer)
        {
            await base.WriteElementsAsync(writer).ConfigureAwait(false);

            //foreach (var import in Imports)
            //    await import.WriteAsync(writer).ConfigureAwait(false);

            if (Types != null)
                await Types.WriteAsync(writer).ConfigureAwait(false);

            foreach (var message in Messages)
                await message.WriteAsync(writer).ConfigureAwait(false);

            foreach (var portType in PortTypes)
                await portType.WriteAsync(writer).ConfigureAwait(false);

            foreach (var binding in Bindings)
                await binding.WriteAsync(writer).ConfigureAwait(false);

            foreach (var service in Services)
                await service.WriteAsync(writer).ConfigureAwait(false);
        }
    }
}