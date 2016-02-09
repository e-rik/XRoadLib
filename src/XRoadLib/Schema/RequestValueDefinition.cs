﻿using System.Reflection;

namespace XRoadLib.Schema
{
    internal class RequestValueDefinition : ContentDefinition
    {
        public OperationDefinition DeclaringOperationDefinition { get; }
        public ParameterInfo ParameterInfo { get; }

        public override string RuntimeName => "request";

        public RequestValueDefinition(ParameterInfo parameterInfo, OperationDefinition declaringOperationDefinition)
        {
            DeclaringOperationDefinition = declaringOperationDefinition;
            ParameterInfo = parameterInfo;
            RuntimeType = NormalizeType(parameterInfo?.ParameterType);

            InitializeContentDefinition(parameterInfo);
        }

        public override string ToString()
        {
            return $"Input value of {ParameterInfo.Member.DeclaringType?.FullName ?? "<null>"}.{ParameterInfo.Member.Name} ({Name})";
        }
    }
}