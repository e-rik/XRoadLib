using System.Web.Services.Description;

namespace XRoadLib.Schema
{
    /// <summary>
    /// Schema exporter for X-Road message protocol version 3.1.
    /// </summary>
    public class SchemaExporterXRoad31 : SchemaExporterXRoadLegacy
    {
        /// <summary>
        /// X-Road standard compliant producer namespace.
        /// </summary>
        public override string ProducerNamespace { get; }

        /// <summary>
        /// Initializes schema exporter for X-Road message protocol version 3.1.
        /// </summary>
        public SchemaExporterXRoad31(string producerName)
            : base(producerName, PrefixConstants.XROAD, NamespaceConstants.XROAD)
        {
            ProducerNamespace = $"http://{producerName}.x-road.ee/producer/";
        }

        /// <summary>
        /// Allows each message protocol implementation to customize service description document
        /// before publishing.
        /// </summary>
        public override void ExportServiceDescription(ServiceDescription serviceDescription)
        {
            base.ExportServiceDescription(serviceDescription);
        }
    }
}