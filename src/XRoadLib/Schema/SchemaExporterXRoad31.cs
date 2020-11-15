using System.Reflection;
using XRoadLib.Headers;

namespace XRoadLib.Schema
{
    /// <summary>
    /// Schema exporter for X-Road message protocol version 3.1.
    /// </summary>
    public class SchemaExporterXRoad31 : SchemaExporterXRoadLegacy
    {
        /// <summary>
        /// Preferred X-Road namespace prefix of the message protocol version.
        /// </summary>
        public override string XRoadPrefix => PrefixConstants.XRoad;

        /// <summary>
        /// X-Road specification namespace of the message protocol version.
        /// </summary>
        public override string XRoadNamespace => NamespaceConstants.XRoad;

        /// <summary>
        /// Initializes schema exporter for X-Road message protocol version 3.1.
        /// </summary>
        public SchemaExporterXRoad31(string producerName, Assembly contractAssembly, string producerNamespace = null)
            : base(producerName, contractAssembly, producerNamespace ?? $"http://{producerName}.x-road.ee/producer/")
        { }

        /// <summary>
        /// Configure SOAP header of the messages.
        /// </summary>
        public override void ExportHeaderDefinition(HeaderDefinition headerDefinition)
        {
            base.ExportHeaderDefinition(headerDefinition);

            headerDefinition.Use<XRoadHeader31>()
                            .WithRequiredHeader(x => x.Consumer)
                            .WithRequiredHeader(x => x.Producer)
                            .WithRequiredHeader(x => x.ServiceName)
                            .WithRequiredHeader(x => x.UserId)
                            .WithRequiredHeader(x => x.Id)
                            .WithRequiredHeader(x => x.UserName)
                            .WithHeaderNamespace(NamespaceConstants.XRoad);
        }
    }
}