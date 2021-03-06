﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

namespace SharpLearning.InputOutput.Serialization
{
    /// <summary>
    /// Generic xml serializer using DataContractSerializer
    /// </summary>
    public sealed class GenericXmlDataContractSerializer : IGenericSerializer
    {
        readonly Type[] m_knownTypes;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="knownTypes"></param>
        public GenericXmlDataContractSerializer(Type[] knownTypes)
        {
            if (knownTypes == null) { throw new ArgumentNullException("knownTypes"); }
            m_knownTypes = knownTypes;
        }

        /// <summary>
        /// 
        /// </summary>
        public GenericXmlDataContractSerializer()
           : this(new Type[0])
        {
        }

        /// <summary>
        /// Serialize data to the provided writer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="writer"></param>
        public void Serialize<T>(T data, Func<TextWriter> writer)
        {
            var settings = new XmlWriterSettings { Indent = true };

            using (var texWriter = writer())
            {
                using (var xmlWriter = XmlWriter.Create(texWriter, settings))
                {
                    var serializer = new DataContractSerializer(typeof(T), m_knownTypes, int.MaxValue,
                        false, true, null, new GenericResolver());

                    serializer.WriteObject(xmlWriter, data);
                }
            }
        }

        /// <summary>
        /// Deserialize data from the provided reader
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <returns></returns>
        public T Deserialize<T>(Func<TextReader> reader)
        {
            using(var textReader = reader())
            {
                using (var xmlReader = XmlReader.Create(textReader))
                {
                    var serializer = new DataContractSerializer(typeof(T), m_knownTypes, int.MaxValue,
                        false, true, null, new GenericResolver());

                    return (T)serializer.ReadObject(xmlReader);
                }
            }
        }

        #region GenericResolver

        /// <summary>
        /// Internal class for generic object type resolving
        /// </summary>
        internal class GenericResolver : DataContractResolver
        {
            const string DefaultNamespace = "global";

            readonly Dictionary<Type, Tuple<string, string>> m_TypeToNames;
            readonly Dictionary<string, Dictionary<string, Type>> m_NamesToType;

            public Type[] KnownTypes
            {
                get
                {
                    return m_TypeToNames.Keys.ToArray();
                }
            }

            public GenericResolver()
                : this(ReflectTypes())
            { }

            public GenericResolver(Type[] typesToResolve)
            {
                m_TypeToNames = new Dictionary<Type, Tuple<string, string>>();
                m_NamesToType = new Dictionary<string, Dictionary<string, Type>>();

                foreach (Type type in typesToResolve)
                {
                    string typeNamespace = GetNamespace(type);
                    string typeName = GetName(type);

                    m_TypeToNames[type] = new Tuple<string, string>(typeNamespace, typeName);

                    if (m_NamesToType.ContainsKey(typeNamespace) == false)
                    {
                        m_NamesToType[typeNamespace] = new Dictionary<string, Type>();
                    }
                    m_NamesToType[typeNamespace][typeName] = type;
                }
            }

            public static GenericResolver Merge(GenericResolver resolver1, GenericResolver resolver2)
            {
                if (resolver1 == null)
                {
                    return resolver2;
                }
                if (resolver2 == null)
                {
                    return resolver1;
                }
                List<Type> types = new List<Type>();
                types.AddRange(resolver1.KnownTypes);
                types.AddRange(resolver2.KnownTypes);

                return new GenericResolver(types.ToArray());
            }

            static string GetNamespace(Type type)
            {
                return type.Namespace ?? DefaultNamespace;
            }

            static string GetName(Type type)
            {
                return type.Name;
            }

            public override Type ResolveName(string typeName, string typeNamespace, Type declaredType, DataContractResolver knownTypeResolver)
            {
                if (m_NamesToType.ContainsKey(typeNamespace))
                {
                    if (m_NamesToType[typeNamespace].ContainsKey(typeName))
                    {
                        return m_NamesToType[typeNamespace][typeName];
                    }
                }
                return knownTypeResolver.ResolveName(typeName, typeNamespace, declaredType, null);
            }

            public override bool TryResolveType(Type type, Type declaredType, DataContractResolver knownTypeResolver, out XmlDictionaryString typeName, out XmlDictionaryString typeNamespace)
            {
                if (m_TypeToNames.ContainsKey(type))
                {
                    XmlDictionary dictionary = new XmlDictionary();
                    typeNamespace = dictionary.Add(m_TypeToNames[type].Item1);
                    typeName = dictionary.Add(m_TypeToNames[type].Item2);

                    return true;
                }
                else
                {
                    return knownTypeResolver.TryResolveType(type, declaredType, null, out typeName, out typeNamespace);
                }
            }

            static Type[] ReflectTypes()
            {
                Assembly[] assemblyReferecnes = AppDomain.CurrentDomain.GetAssemblies();

                List<Type> types = new List<Type>();

                foreach (Assembly assembly in assemblyReferecnes)
                {
                    Type[] typesInReferencedAssembly = GetTypes(assembly);
                    types.AddRange(typesInReferencedAssembly);
                }

                return types.ToArray();
            }


            static Type[] GetTypes(Assembly assembly, bool publicOnly = true)
            {
                Type[] allTypes = assembly.GetTypes();

                List<Type> types = new List<Type>();

                foreach (Type type in allTypes)
                {
                    if (type.IsEnum == false &&
                       type.IsInterface == false &&
                       type.IsGenericTypeDefinition == false)
                    {
                        if (publicOnly == true && type.IsPublic == false)
                        {
                            if (type.IsNested == false)
                            {
                                continue;
                            }
                            if (type.IsNestedPrivate == true)
                            {
                                continue;
                            }
                        }
                        types.Add(type);
                    }
                }
                return types.ToArray();
            }
        }

        #endregion
    }
}
