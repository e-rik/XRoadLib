#if !NET40

using System;
using Microsoft.AspNetCore.Http;

namespace XRoadLib.Handler
{
    /// <summary>
    /// Handle X-Road service description request on AspNetCore platform.
    /// </summary>
    public class XRoadWsdlHandler : XRoadHandlerBase
    {
        private readonly XRoadProtocol protocol;

        /// <summary>
        /// Initialize new handler for certain protocol.
        /// </summary>
        public XRoadWsdlHandler(XRoadProtocol protocol)
        {
            if (protocol == null)
                throw new ArgumentNullException(nameof(protocol));
            this.protocol = protocol;
        }

        /// <summary>
        /// Handle service description request.
        /// </summary>
        public override void HandleRequest(HttpContext context)
        {
            protocol.WriteServiceDescription(context.Response.Body);
        }
    }
}

#endif