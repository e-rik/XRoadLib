﻿using System;
using System.Collections.Generic;
using System.Web;
using XRoadLib.Serialization;

namespace XRoadLib.Extensions.AspNet
{
    public static class XRoadMessageExtensions
    {
        /// <summary>
        /// Loads X-Road message contents from request message.
        /// </summary>
        public static void LoadRequest(this XRoadMessage message, HttpContext httpContext, string storagePath, IEnumerable<IServiceManager> serviceManagers)
        {
            message.LoadRequest(httpContext.Request.InputStream, httpContext.Request.Headers.GetContentTypeHeader(), storagePath, serviceManagers);
        }

        /// <summary>
        /// Serializes X-Road message into specified HTTP context response.
        /// </summary>
        public static void SaveTo(this XRoadMessage message, HttpContext httpContext)
        {

            var outputStream = httpContext.Response.OutputStream;
            var appendHeader = new Action<string, string>(httpContext.Response.AppendHeader);

            using (var writer = new XRoadMessageWriter(outputStream))
                writer.Write(message, contentType => httpContext.Response.ContentType = contentType, appendHeader);
        }
    }
}