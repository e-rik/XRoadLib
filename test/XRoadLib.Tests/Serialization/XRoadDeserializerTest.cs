﻿using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using XRoadLib.Extensions;
using XRoadLib.Schema;
using XRoadLib.Serialization;
using XRoadLib.Serialization.Mapping;
using XRoadLib.Serialization.Template;
using XRoadLib.Soap;
using XRoadLib.Tests.Contract;
using XRoadLib.Tests.Contract.Wsdl;
using Xunit;

namespace XRoadLib.Tests.Serialization
{
    public class XRoadDeserializerTest
    {
        private const uint DtoVersion = 3;

        private static readonly ISerializer Serializer20 = Globals.ServiceManager20.GetSerializer(DtoVersion);
        private static readonly ISerializer Serializer31 = Globals.ServiceManager31.GetSerializer(DtoVersion);
        private static readonly IServiceMap ServiceMap20 = Serializer20.GetServiceMap("Service1");
        private static readonly IServiceMap ServiceMap31 = Serializer31.GetServiceMap("Service1");
        private static readonly IServiceMap Service3Map31 = Serializer31.GetServiceMap("Service3");

        private static readonly IMessageFormatter MessageFormatter = new SoapMessageFormatter();

        [Fact]
        public void CanHandleOptionalParameters()
        {
            const string templateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                                       + "<keha>"
                                       + "  <Param1>"
                                       + "    <Property1 />"
                                       + "  </Param1>"
                                       + "  <Param2 />"
                                       + "  <Param3 />"
                                       + "</keha>";

            const string contentXml = "<keha>"
                                      + "  <Param1>"
                                      + "    <Property1>123</Property1>"
                                      + "  </Param1>"
                                      + "</keha>";

            var inputObject = DeserializeRequest20(templateXml, contentXml);
            Assert.IsType<Service1Request>(inputObject);

            var request = (Service1Request)inputObject;
            Assert.True(request.IsSpecified("Param1"));
            Assert.False(request.IsSpecified("Param2"));
            Assert.False(request.IsSpecified("Param3"));
            Assert.NotNull(request.Param1);
            Assert.Null(request.Param2);
            Assert.Null(request.Param3);
            Assert.Equal(123, request.Param1.Property1);
        }

        [Fact]
        public void CanHandleArrayType()
        {
            const string templateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                                       + "<keha>"
                                       + "  <Param1>"
                                       + "    <Property1 />"
                                       + "    <Property2>"
                                       + "      <Value1 />"
                                       + "    </Property2>"
                                       + "  </Param1>"
                                       + "  <Param2 />"
                                       + "  <Param3 />"
                                       + "</keha>";

            const string contentXml = "<keha>"
                                      + "  <Param1>"
                                      + "    <Property1>123</Property1>"
                                      + "    <Property2>"
                                      + "      <item><Value1>102715</Value1></item>"
                                      + "      <item><Value1>102716</Value1></item>"
                                      + "    </Property2>"
                                      + "  </Param1>"
                                      + "</keha>";

            var inputObject = DeserializeRequest20(templateXml, contentXml);
            Assert.IsType<Service1Request>(inputObject);

            var request = (Service1Request)inputObject;
            Assert.True(request.IsSpecified("Param1"));
            Assert.False(request.IsSpecified("Param2"));
            Assert.False(request.IsSpecified("Param3"));
            Assert.NotNull(request.Param1);
            Assert.Null(request.Param2);
            Assert.Null(request.Param3);
            Assert.Equal(123, request.Param1.Property1);
            Assert.NotNull(request.Param1.Property2);
            Assert.Equal(2, request.Param1.Property2.Length);
            Assert.NotNull(request.Param1.Property2[0]);
            Assert.Equal(102715L, request.Param1.Property2[0].Value1);
            Assert.NotNull(request.Param1.Property2[1]);
            Assert.Equal(102716L, request.Param1.Property2[1].Value1);
        }

        [Fact]
        public void CanHandleEmptyArray()
        {
            const string templateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                                       + "<keha>"
                                       + "  <Param1>"
                                       + "    <Property1 />"
                                       + "    <Property2>"
                                       + "      <Value1 />"
                                       + "    </Property2>"
                                       + "  </Param1>"
                                       + "  <Param2 />"
                                       + "  <Param3 />"
                                       + "</keha>";

            const string contentXml = "<keha>"
                                      + "  <Param1>"
                                      + "    <Property1>123</Property1>"
                                      + "    <Property2 />"
                                      + "  </Param1>"
                                      + "</keha>";

            var inputObject = DeserializeRequest20(templateXml, contentXml);
            Assert.IsType<Service1Request>(inputObject);

            var request = (Service1Request)inputObject;
            Assert.True(request.IsSpecified("Param1"));
            Assert.False(request.IsSpecified("Param2"));
            Assert.False(request.IsSpecified("Param3"));
            Assert.NotNull(request.Param1);
            Assert.Null(request.Param2);
            Assert.Null(request.Param3);
            Assert.Equal(123, request.Param1.Property1);
            Assert.NotNull(request.Param1.Property2);
            Assert.Empty(request.Param1.Property2);
        }

        [Fact]
        public void CanHandleArrayNullValue()
        {
            const string templateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                                       + "<keha>"
                                       + "  <Param1>"
                                       + "    <Property1 />"
                                       + "    <Property2>"
                                       + "      <Value1 />"
                                       + "    </Property2>"
                                       + "  </Param1>"
                                       + "  <Param2 />"
                                       + "  <Param3 />"
                                       + "</keha>";

            const string contentXml = "<keha xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                                      + "  <Param1>"
                                      + "    <Property1>123</Property1>"
                                      + "    <Property2 xsi:nil=\"1\" />"
                                      + "  </Param1>"
                                      + "</keha>";

            var inputObject = DeserializeRequest20(templateXml, contentXml);
            Assert.IsType<Service1Request>(inputObject);

            var request = (Service1Request)inputObject;
            Assert.True(request.IsSpecified("Param1"));
            Assert.False(request.IsSpecified("Param2"));
            Assert.False(request.IsSpecified("Param3"));
            Assert.NotNull(request.Param1);
            Assert.Null(request.Param2);
            Assert.Null(request.Param3);
            Assert.Equal(123, request.Param1.Property1);
            Assert.Null(request.Param1.Property2);
        }

        [Fact]
        public void CanHandleMultipleSimpleProperties()
        {
            const string templateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                                       + "<keha>"
                                       + "  <Param1>"
                                       + "    <Property1 />"
                                       + "    <Property3 />"
                                       + "    <Property2>"
                                       + "      <Value1 />"
                                       + "    </Property2>"
                                       + "  </Param1>"
                                       + "  <Param2 />"
                                       + "  <Param3 />"
                                       + "</keha>";

            const string contentXml = "<keha xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                                      + "  <Param1>"
                                      + "    <Property1>123</Property1>"
                                      + "    <Property3>some value</Property3>"
                                      + "    <Property2>"
                                      + "      <item><Value1>102715</Value1></item>"
                                      + "      <item><Value1>102716</Value1></item>"
                                      + "    </Property2>"
                                      + "  </Param1>"
                                      + "</keha>";

            var inputObject = DeserializeRequest20(templateXml, contentXml);
            Assert.IsType<Service1Request>(inputObject);

            var request = (Service1Request)inputObject;
            Assert.True(request.IsSpecified("Param1"));
            Assert.False(request.IsSpecified("Param2"));
            Assert.False(request.IsSpecified("Param3"));
            Assert.NotNull(request.Param1);
            Assert.Null(request.Param2);
            Assert.Null(request.Param3);
            Assert.Equal(123, request.Param1.Property1);
            Assert.Equal("some value", request.Param1.Property3);
            Assert.NotNull(request.Param1.Property2);
            Assert.Equal(2, request.Param1.Property2.Length);
            Assert.NotNull(request.Param1.Property2[0]);
            Assert.Equal(102715L, request.Param1.Property2[0].Value1);
            Assert.NotNull(request.Param1.Property2[1]);
            Assert.Equal(102716L, request.Param1.Property2[1].Value1);
        }

        [Fact]
        public void CanHandleMultipleParameters()
        {
            const string templateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                                       + "<keha>"
                                       + "  <Param1>"
                                       + "    <Property1 />"
                                       + "    <Property3 />"
                                       + "    <Property2>"
                                       + "      <Value1 />"
                                       + "    </Property2>"
                                       + "  </Param1>"
                                       + "  <Param2 />"
                                       + "  <Param3>"
                                       + "    <Subject>"
                                       + "      <Name />"
                                       + "    </Subject>"
                                       + "  </Param3>"
                                       + "</keha>";

            const string contentXml = "<request xmlns:tns=\"http://test-producer.x-road.ee/producer/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                                      + "  <Param1>"
                                      + "    <Property1>123</Property1>"
                                      + "    <Property3>some value</Property3>"
                                      + "    <Property2>"
                                      + "      <item><Value1>102715</Value1></item>"
                                      + "      <item><Value1>102716</Value1></item>"
                                      + "    </Property2>"
                                      + "  </Param1>"
                                      + "  <Param3>"
                                      + "    <Subject xsi:type=\"tns:Person\">"
                                      + "      <Name>Vello</Name>"
                                      + "    </Subject>"
                                      + "  </Param3>"
                                      + "</request>";

            var inputObject = DeserializeRequest31(templateXml, contentXml);
            Assert.IsType<Service1Request>(inputObject);

            var request = (Service1Request)inputObject;
            Assert.True(request.IsSpecified("Param1"));
            Assert.False(request.IsSpecified("Param2"));
            Assert.True(request.IsSpecified("Param3"));
            Assert.NotNull(request.Param1);
            Assert.Null(request.Param2);
            Assert.NotNull(request.Param3);
            Assert.Equal(123, request.Param1.Property1);
            Assert.Equal("some value", request.Param1.Property3);
            Assert.NotNull(request.Param1.Property2);
            Assert.Equal(2, request.Param1.Property2.Length);
            Assert.NotNull(request.Param1.Property2[0]);
            Assert.Equal(102715L, request.Param1.Property2[0].Value1);
            Assert.NotNull(request.Param1.Property2[1]);
            Assert.Equal(102716L, request.Param1.Property2[1].Value1);
            Assert.NotNull(request.Param3.Subject);
            Assert.IsType<Person>(request.Param3.Subject);
            Assert.Equal("Vello", request.Param3.Subject.Name);
        }

        [Fact]
        public void CannotDeserializeAbstractType()
        {
            const string templateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                                       + "<keha>"
                                       + "  <Param1 />"
                                       + "  <Param2 />"
                                       + "  <Param3>"
                                       + "    <Subject />"
                                       + "  </Param3>"
                                       + "</keha>";

            const string contentXml = "<keha>"
                                      + "  <Param3>"
                                      + "    <Subject>"
                                      + "      <Name>Vello</Name>"
                                      + "    </Subject>"
                                      + "  </Param3>"
                                      + "</keha>";

            var exception = Assert.Throws<InvalidQueryException>(() => DeserializeRequest20(templateXml, contentXml));
            Assert.Equal("The type '{http://producers.test-producer.xtee.riik.ee/producer/test-producer}Subject' is abstract, type attribute is required to specify target type.", exception.Message);
        }

        [Fact]
        public void CanDeserializeAbstractTypeWhenNull()
        {
            const string templateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                                       + "<keha>"
                                       + "  <Param1 />"
                                       + "  <Param2 />"
                                       + "  <Param3>"
                                       + "    <Subject />"
                                       + "  </Param3>"
                                       + "</keha>";

            const string contentXml = "<keha xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                                      + "  <Param3>"
                                      + "    <Subject xsi:nil=\"true\" />"
                                      + "  </Param3>"
                                      + "</keha>";

            var inputObject = DeserializeRequest20(templateXml, contentXml);
            Assert.IsType<Service1Request>(inputObject);

            var request = (Service1Request)inputObject;
            Assert.False(request.IsSpecified("Param1"));
            Assert.False(request.IsSpecified("Param2"));
            Assert.True(request.IsSpecified("Param3"));
            Assert.Null(request.Param1);
            Assert.Null(request.Param2);
            Assert.NotNull(request.Param3);
            Assert.Null(request.Param3.Subject);
        }

        [Fact]
        public void UnderstandsParameterTypeAttribute()
        {
            const string templateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                                       + "<keha>"
                                       + "  <Param1>"
                                       + "    <Property1 />"
                                       + "    <Property3 />"
                                       + "  </Param1>"
                                       + "  <Param2 />"
                                       + "  <Param3 />"
                                       + "</keha>";

            const string contentXml = "<request xmlns:tns=\"http://test-producer.x-road.ee/producer/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                                      + "  <Param1 xsi:type=\"tns:InheritsParamType1\">"
                                      + "    <Property1>467</Property1>"
                                      + "    <Property3>hello</Property3>"
                                      + "  </Param1>"
                                      + "</request>";

            var inputObject = DeserializeRequest31(templateXml, contentXml);
            Assert.IsType<Service1Request>(inputObject);

            var request = (Service1Request)inputObject;
            Assert.True(request.IsSpecified("Param1"));
            Assert.False(request.IsSpecified("Param2"));
            Assert.False(request.IsSpecified("Param3"));
            Assert.NotNull(request.Param1);
            Assert.Null(request.Param2);
            Assert.Null(request.Param3);
            Assert.IsType<InheritsParamType1>(request.Param1);

            var param1 = (InheritsParamType1)request.Param1;
            Assert.Equal(467, param1.Property1);
            Assert.Equal("hello", param1.Property3);
        }

        [Fact]
        public void CanHandleEmptyContent()
        {
            const string templateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                                       + "<keha>"
                                       + "  <param1 />"
                                       + "  <param2 />"
                                       + "  <param3 />"
                                       + "</keha>";

            const string contentXml = "<keha />";

            var inputObject = DeserializeRequest20(templateXml, contentXml);
            Assert.IsType<Service1Request>(inputObject);

            var request = (Service1Request)inputObject;
            Assert.False(request.IsSpecified("param1"));
            Assert.False(request.IsSpecified("param2"));
            Assert.False(request.IsSpecified("param3"));
            Assert.Null(request.Param1);
            Assert.Null(request.Param2);
            Assert.Null(request.Param3);
        }

        [Fact]
        public void CannotDeserializeMessageWhenMimeContentIsMissing()
        {
            const string templateXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
                                       + "<keha>"
                                       + "  <Param1>"
                                       + "    <MimeContent>"
                                       + "      <Value />"
                                       + "    </MimeContent>"
                                       + "  </Param1>"
                                       + "  <Param2 />"
                                       + "  <Param3 />"
                                       + "</keha>";

            const string contentXml = "<keha xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:SOAP-ENC=\"http://schemas.xmlsoap.org/soap/encoding/\">"
                                      + "  <Param1>"
                                      + "    <MimeContent>"
                                      + "      <Value xsi:type=\"SOAP-ENC:base64\" href=\"cid:KcGPT5EOP0BC0DXQ5wmEFA==\" />"
                                      + "    </MimeContent>"
                                      + "  </Param1>"
                                      + "</keha>";

            var exception = Assert.Throws<InvalidQueryException>(() => DeserializeRequest20(templateXml, contentXml));
            Assert.Equal("MIME multipart message does not contain message part with ID `cid:KcGPT5EOP0BC0DXQ5wmEFA==`.", exception.Message);
        }

        [Fact]
        public void CanDeserializeAnonymousType()
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            writer.WriteLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            writer.WriteLine(@"<entity xsi:type=""tns:ContainerType"" xmlns:xsi=""{0}"" xmlns:tns=""{1}"">", NamespaceConstants.Xsi, Globals.ServiceManager20.ProducerNamespace);
            writer.WriteLine(@"<AnonymousProperty>");
            writer.WriteLine(@"<Property1 xsi:type=""xsd:string"" xmlns:xsd=""{0}"">1</Property1>", NamespaceConstants.Xsd);
            writer.WriteLine(@"<Property2 xsi:type=""xsd:string"" xmlns:xsd=""{0}"">2</Property2>", NamespaceConstants.Xsd);
            writer.WriteLine(@"<Property3 xsi:type=""xsd:string"" xmlns:xsd=""{0}"">3</Property3>", NamespaceConstants.Xsd);
            writer.WriteLine(@"</AnonymousProperty>");
            writer.WriteLine(@"<KnownProperty xsi:type=""xsd:string"" xmlns:xsd=""{0}"">value</KnownProperty>", NamespaceConstants.Xsd);
            writer.WriteLine(@"</entity>");
            writer.Flush();

            stream.Position = 0;
            using var reader = XmlReader.Create(stream);

            Assert.True(reader.MoveToElement(0));

            var typeMap = Serializer31.GetTypeMap(typeof(ContainerType));

            using var message = new XRoadMessage();
            var entity = typeMap.Deserialize(reader, XRoadXmlTemplate.EmptyNode, Globals.GetTestDefinition(typeof(ContainerType)), message);
            Assert.NotNull(entity);
            Assert.IsType<ContainerType>(entity);

            var container = (ContainerType)entity;
            Assert.Equal("value", container.KnownProperty);
            Assert.NotNull(container.AnonymousProperty);
            Assert.Equal("1", container.AnonymousProperty.Property1);
            Assert.Equal("2", container.AnonymousProperty.Property2);
            Assert.Equal("3", container.AnonymousProperty.Property3);
        }

        [Fact]
        public void AnonymousTypeShouldNotHaveExplicitType()
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            writer.WriteLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            writer.WriteLine(@"<entity xsi:type=""tns:ContainerType"" xmlns:xsi=""{0}"" xmlns:tns=""{1}"">", NamespaceConstants.Xsi, Globals.ServiceManager20.ProducerNamespace);
            writer.WriteLine(@"<AnonymousProperty xsi:type=""Test"">");
            writer.WriteLine(@"<Property1 xsi:type=""xsd:string"" xmlns:xsd=""{0}"">1</Property1>", NamespaceConstants.Xsd);
            writer.WriteLine(@"<Property2 xsi:type=""xsd:string"" xmlns:xsd=""{0}"">2</Property2>", NamespaceConstants.Xsd);
            writer.WriteLine(@"<Property3 xsi:type=""xsd:string"" xmlns:xsd=""{0}"">3</Property3>", NamespaceConstants.Xsd);
            writer.WriteLine(@"</AnonymousProperty>");
            writer.WriteLine(@"<KnownProperty xsi:type=""xsd:string"" xmlns:xsd=""{0}"">value</KnownProperty>", NamespaceConstants.Xsd);
            writer.WriteLine(@"</entity>");
            writer.Flush();

            var anonymousProperty = typeof(ContainerType).GetProperty(nameof(ContainerType.AnonymousProperty));

            stream.Position = 0;
            using var reader = XmlReader.Create(stream);

            Assert.True(reader.MoveToElement(0));
            var typeMap = Serializer31.GetTypeMap(typeof(ContainerType));

            using var message = new XRoadMessage();

            var exception = Assert.Throws<UnknownTypeException>(() => typeMap.Deserialize(reader, XRoadXmlTemplate.EmptyNode, Globals.GetTestDefinition(typeof(ContainerType)), message));
            Assert.Equal("Expected anonymous type, but `Test` was given.", exception.Message);
            Assert.True(exception.TypeDefinition.IsAnonymous);
            Assert.Same(exception.TypeDefinition.Type, anonymousProperty?.PropertyType);
            Assert.IsType<PropertyDefinition>(exception.ParticleDefinition);
            Assert.Same(((PropertyDefinition)exception.ParticleDefinition).PropertyInfo, anonymousProperty);
            Assert.Equal(exception.QualifiedName, XName.Get("Test"));
        }

        [Fact]
        public void CanDeserializedMergedArrayWithEmptyContent()
        {
            var contentXml = "<request xmlns:tns=\"http://test-producer.x-road.ee/producer/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
                             + "  <Value />"
                             + "  <Code />"
                             + "  <Code />"
                             + "  <Code />"
                             + "  <Value2>Joy</Value2>"
                             + "</request>";

            var inputObject = DeserializeRequest31(null, contentXml, Service3Map31, "Service3");
            Assert.IsType<TestMergedArrayContent>(inputObject);

            var request = (TestMergedArrayContent)inputObject;
            Assert.True(request.IsSpecified("Value"));
            Assert.True(request.IsSpecified("Codes"));
            Assert.True(request.IsSpecified("Value2"));

            Assert.NotNull(request.Value);
            Assert.Equal("", request.Value);

            Assert.NotNull(request.Codes);
            Assert.Equal(3, request.Codes.Length);
            Assert.Equal(request.Codes, new[] { "", "", "" });

            Assert.NotNull(request.Value2);
            Assert.Equal("Joy", request.Value2);
        }

        private static object DeserializeRequest20(string templateXml, string contentXml)
        {
            var template = new XRoadXmlTemplate(templateXml, typeof(IService).GetTypeInfo().GetMethod("Service1"));

            return DeserializeRequest(contentXml, Globals.ServiceManager20, "Service1", (msgr, xmlr) =>
            {
                var message = Globals.ServiceManager20.CreateMessage();
                message.XmlTemplate = template;

                using (message)
                {
                    msgr.Read(message);
                    MessageFormatter.MoveToPayload(xmlr, XName.Get("Service1", Globals.ServiceManager20.ProducerNamespace));
                    return ServiceMap20.DeserializeRequest(xmlr, message);
                }
            });
        }

        private static object DeserializeRequest31(string templateXml, string contentXml, IServiceMap serviceMap = null, string serviceName = "Service1")
        {
            serviceMap ??= ServiceMap31;
            var template = string.IsNullOrEmpty(templateXml) ? null : new XRoadXmlTemplate(templateXml, typeof(IService).GetTypeInfo().GetMethod(serviceName));
            return DeserializeRequest(contentXml, Globals.ServiceManager31, serviceName, (msgr, xmlr) =>
            {
                var message = Globals.ServiceManager31.CreateMessage();
                message.XmlTemplate = template;

                using (message)
                {
                    msgr.Read(message);
                    MessageFormatter.MoveToPayload(xmlr, XName.Get(serviceName, Globals.ServiceManager31.ProducerNamespace));
                    return serviceMap.DeserializeRequest(xmlr, message);
                }
            });
        }

        private static object DeserializeRequest(string contentXml, IServiceManager protocol, string serviceName, Func<XRoadMessageReader, XmlReader, object> deserializeMessage)
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);

            writer.WriteLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            writer.WriteLine($"<soapenv:Envelope xmlns:soapenv=\"{NamespaceConstants.SoapEnv}\" soapenv:encodingStyle=\"{NamespaceConstants.SoapEnc}\">");
            writer.WriteLine(@"<soapenv:Body>");
            writer.WriteLine($"<tns:{serviceName} xmlns:tns=\"{protocol.ProducerNamespace}\">");
            writer.WriteLine(contentXml);
            writer.WriteLine($"@</tns:{serviceName}>");
            writer.WriteLine("@</soapenv:Body>");
            writer.WriteLine("@</soapenv:Envelope>");
            writer.Flush();

            stream.Position = 0;

            using var reader = XmlReader.Create(stream);
            using var messageReader = new XRoadMessageReader(stream, MessageFormatter, "text/xml; charset=UTF-8", null, new[] { protocol });

            return deserializeMessage(messageReader, reader);
        }
    }
}