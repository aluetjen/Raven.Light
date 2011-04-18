using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Raven.Json.Linq;
using Raven.Light.Converters;
using Raven.Light.Impl;
using Raven.Light.Json;
using Raven.Light.Util;

namespace Raven.Light.Conventions
{
	/// <summary>
	/// The set of conventions used by the <see cref="EmbeddedDocumentStore"/> which allow the users to customize
	/// the way the Raven client API behaves
	/// </summary>
	public class DocumentConvention
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DocumentConvention"/> class.
		/// </summary>
		public DocumentConvention()
		{
			IdentityTypeConvertors = new List<ITypeConverter>
			{
				new GuidConverter(),
				new Int32Converter(),
				new Int64Converter(),
			};
			ShouldCacheRequest = url => true;
			FindIdentityProperty = q => q.Name == "Id";
			FindClrType = (id, doc, metadata) => metadata.Value<string>(Constants.RavenClrType);
			FindFullDocumentKeyFromValueTypeIdentifier = DefaultFindFullDocumentKeyFromValueTypeIdentifier;
			FindIdentityPropertyNameFromEntityName = entityName => "Id";
			FindTypeTagName = t => DefaultTypeTagName(t);
			FindPropertyNameForIndex = (indexedType, indexedName, path, prop) => prop;
			FindPropertyNameForDynamicIndex = (indexedType, indexedName, path, prop) => path + prop;
			IdentityPartsSeparator = "/";
			JsonContractResolver = new DefaultRavenContractResolver(shareCache: true)
			{
				DefaultMembersSearchFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
			};
			MaxNumberOfRequestsPerSession = 30;
			CustomizeJsonSerializer = serializer => { };
		}

		///<summary>
		/// Find the full document name assuming that we are using the standard conventions
		/// for generating a document key
		///</summary>
		///<returns></returns>
		public string DefaultFindFullDocumentKeyFromValueTypeIdentifier(ValueType id, Type type)
		{
			string idPart;
			var converter = IdentityTypeConvertors.FirstOrDefault(x => x.CanConvertFrom(id.GetType()));
			if (converter != null)
			{
				idPart = converter.ConvertFrom(id);
			}
			else
			{
				idPart = id.ToString();
			}
			return GetTypeTagName(type) + IdentityPartsSeparator + idPart;
		}

		/// <summary>
		/// Register an action to customize the json serializer used by the <see cref="DocumentStore"/>
		/// </summary>
		public Action<JsonSerializer> CustomizeJsonSerializer { get; set; }

		///<summary>
		/// A list of type converters that can be used to translate the document key (string)
		/// to whatever type it is that is used on the entity, if the type isn't already a string
		///</summary>
		public List<ITypeConverter> IdentityTypeConvertors { get; set; }

		/// <summary>
		/// Gets or sets the identity parts separator used by the hilo generators
		/// </summary>
		/// <value>The identity parts separator.</value>
		public string IdentityPartsSeparator { get; set; }

		/// <summary>
		/// Gets or sets the default max number of requests per session.
		/// </summary>
		/// <value>The max number of requests per session.</value>
		public int MaxNumberOfRequestsPerSession { get; set; }

		/// <summary>
		/// Generates the document key using identity.
		/// </summary>
		/// <param name="conventions">The conventions.</param>
		/// <param name="entity">The entity.</param>
		/// <returns></returns>
		public static string GenerateDocumentKeyUsingIdentity(DocumentConvention conventions, object entity)
		{
			return conventions.FindTypeTagName(entity.GetType()).ToLower() + "/";
		}

		private static IDictionary<Type, string> cachedDefaultTypeTagNames = new Dictionary<Type, string>();

		/// <summary>
		/// Get the default tag name for the specified type.
		/// </summary>
		public static string DefaultTypeTagName(Type t)
		{
			string result;
			if (cachedDefaultTypeTagNames.TryGetValue(t, out result))
				return result;

			if (t.Name.Contains("<>"))
				return null;
			if (t.IsGenericType)
			{
				var name = t.GetGenericTypeDefinition().Name;
				if (name.Contains("`"))
				{
					name = name.Substring(0, name.IndexOf("`"));
				}
				var sb = new StringBuilder(Inflector.Pluralize(name));
				foreach (var argument in t.GetGenericArguments())
				{
					sb.Append("Of")
						.Append(DefaultTypeTagName(argument));
				}
				result = sb.ToString();
			}
			else
			{
				result = Inflector.Pluralize(t.Name);
			}
			var temp = new Dictionary<Type, string>(cachedDefaultTypeTagNames);
			temp[t] = result;
			cachedDefaultTypeTagNames = temp;
			return result;
		}

		/// <summary>
		/// Gets the name of the type tag.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns></returns>
		public string GetTypeTagName(Type type)
		{
			return FindTypeTagName(type) ?? DefaultTypeTagName(type);
		}

		/// <summary>
		/// Generates the document key.
		/// </summary>
		/// <param name="entity">The entity.</param>
		/// <returns></returns>
		public string GenerateDocumentKey(object entity)
		{
			return DocumentKeyGenerator(entity);
		}

		/// <summary>
		/// Gets the identity property.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns></returns>
		public PropertyInfo GetIdentityProperty(Type type)
		{
			return type.GetProperties().FirstOrDefault(FindIdentityProperty);
		}

		/// <summary>
		/// Gets or sets the function to find the clr type of a document.
		/// </summary>
		public Func<string, RavenJObject, RavenJObject, string> FindClrType { get; set; }


		/// <summary>
		/// Gets or sets the function to find the full document key based on the type of a document
		/// and the value type identifier (just the numeric part of the id).
		/// </summary>
		public Func<ValueType, Type, string> FindFullDocumentKeyFromValueTypeIdentifier { get; set; }

		/// <summary>
		/// Gets or sets the json contract resolver.
		/// </summary>
		/// <value>The json contract resolver.</value>
		public IContractResolver JsonContractResolver { get; set; }

		/// <summary>
		/// Gets or sets the function to find the type tag.
		/// </summary>
		/// <value>The name of the find type tag.</value>
		public Func<Type, string> FindTypeTagName { get; set; }

		/// <summary>
		/// Gets or sets the function to find the indexed property name
		/// given the indexed document type, the index name, the current path and the property path.
		/// </summary>
		public Func<Type, string, string, string, string> FindPropertyNameForIndex { get; set; }

		/// <summary>
		/// Gets or sets the function to find the indexed property name
		/// given the indexed document type, the index name, the current path and the property path.
		/// </summary>
		public Func<Type, string, string, string, string> FindPropertyNameForDynamicIndex { get; set; }

		/// <summary>
		/// Whatever or not RavenDB should cache the request to the specified url.
		/// </summary>
		public Func<string, bool> ShouldCacheRequest { get; set; }

		/// <summary>
		/// Gets or sets the function to find the identity property.
		/// </summary>
		/// <value>The find identity property.</value>
		public Func<PropertyInfo, bool> FindIdentityProperty { get; set; }

		/// <summary>
		/// Get or sets the function to get the identity property name from the entity name
		/// </summary>
		public Func<string, string> FindIdentityPropertyNameFromEntityName { get; set; }

		/// <summary>
		/// Gets or sets the document key generator.
		/// </summary>
		/// <value>The document key generator.</value>
		public Func<object, string> DocumentKeyGenerator { get; set; }

		/// <summary>
		/// Creates the serializer.
		/// </summary>
		/// <returns></returns>
		public JsonSerializer CreateSerializer()
		{
			var jsonSerializer = new JsonSerializer
			{
				ObjectCreationHandling = ObjectCreationHandling.Replace,
				ContractResolver = JsonContractResolver,
				TypeNameHandling = TypeNameHandling.Auto,
				TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
				ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
				Converters =
					{
						new JsonEnumConverter(),
						new JsonDateTimeISO8601Converter(),
                        new JsonDateTimeOffsetConverter(),
                        new JsonValueTypeConverter<int>(int.TryParse),
                        new JsonValueTypeConverter<long>(long.TryParse),
                        new JsonValueTypeConverter<decimal>(decimal.TryParse),
                        new JsonValueTypeConverter<double>(double.TryParse),
                        new JsonValueTypeConverter<float>(float.TryParse),
                        new JsonValueTypeConverter<short>(short.TryParse),
						new JsonMultiDimensionalArrayConverter(),
					}
			};
			CustomizeJsonSerializer(jsonSerializer);
			return jsonSerializer;
		}

		/// <summary>
		/// Get the CLR type (if exists) from the document
		/// </summary>
		public string GetClrType(string id, RavenJObject document, RavenJObject metadata)
		{
			return FindClrType(id, document, metadata);
		}
	}
}