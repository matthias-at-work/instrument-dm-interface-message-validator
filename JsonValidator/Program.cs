using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace JsonValidator
{
    static class Program
    {
        // Set the path to the repo here!
        private static readonly string BaseDir = @"C:\Code\GitHub\instrument-dm-interface";

        private static readonly string SchemaDir = Path.Combine(BaseDir, @"schema");
        private static readonly Uri BaseUri = new Uri(@"http://roche.com/rmd/");

        static void Main()
        {
            IList<string> invalidFileNames = new List<string>();

            // create a resolver with all type-schemas loaded.
            JSchemaPreloadedResolver schemaResolver = CreateResolver(Path.Combine(SchemaDir, @"X800\types"));
            Console.WriteLine("SchemaResolver was initialized with the following types:");
            foreach (Uri uri in schemaResolver.PreloadedUris)
            {
                Console.WriteLine("  {0}", uri);
            }

            // load all examples and validate them.
            Console.WriteLine();
            Console.WriteLine("Validating examples");
            IEnumerable<string> jsonMessageFileNames = Directory.GetFiles(Path.Combine(BaseDir, "examples"), @"*.json", SearchOption.AllDirectories);

            foreach (string jsonMessageFileName in jsonMessageFileNames)
            {
                JObject jsonMessage = LoadJsonFile(jsonMessageFileName);
                Uri referencedMessageSchema = GetReferencedSchema(jsonMessage);

                JSchema messageSchema = LoadMessageSchema(referencedMessageSchema, schemaResolver);
                bool isValid = jsonMessage.IsValid(messageSchema);

                Console.WriteLine("  {0} :  {1}", isValid ? "Valid" : "Invalid", jsonMessageFileName);

                if (!isValid)
                {
                    invalidFileNames.Add(jsonMessageFileName);
                }
            }

            Console.WriteLine();
            if (invalidFileNames.Count == 0)
            {
                Console.WriteLine("No invalid examples found!");
            }
            else
            {
                Console.WriteLine("{0} invalid files found:", invalidFileNames.Count);
                foreach (string invalidFileName in invalidFileNames)
                {
                    Console.WriteLine(" - {0}", invalidFileName);
                }
            }

            Console.ReadKey();
        }

        private static Uri GetReferencedSchema(JObject jsonObject)
        {
            var schemaRef = jsonObject.Properties().FirstOrDefault(p => p.Name.Equals("schema"));
            if (schemaRef == null)
            {
                return null;
            }

            return new Uri(schemaRef.Value.ToString());
        }

        private static JSchema LoadMessageSchema(Uri messageUri, JSchemaResolver resolver)
        {
            if (!BaseUri.IsBaseOf(messageUri))
            {
                throw new Exception("Invalid reference URI.");
            }

            Uri relativeUri = BaseUri.MakeRelativeUri(messageUri);
            string messageSchemaFileName = Path.Combine(SchemaDir, relativeUri.ToString());
            if (!File.Exists(messageSchemaFileName))
            {
                throw new Exception("The URI doesn't refer to an existing message-schema.");
            }

            return LoadSchema(resolver, messageSchemaFileName);
        }

        private static JObject LoadJsonFile(string fileName)
        {
            using (StreamReader file = File.OpenText(fileName))
            {
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    return JObject.Load(reader);
                }
            }
        }

        private static JSchema LoadSchema(JSchemaResolver resolver, string schemaFileName)
        {
            using (StreamReader file = File.OpenText(schemaFileName))
            {
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    return JSchema.Load(reader, resolver);
                }
            }
        }

        private static JSchemaPreloadedResolver CreateResolver(string typesDirectory)
        {
            JSchemaPreloadedResolver resolver = new JSchemaPreloadedResolver();

            // load the json schema #04 - all schemas reference this.
            JSchema rootSchema = ReadJsonSchemaDraft04();

            // load all types.
            IEnumerable<string> schemaFileNames = Directory.GetFiles(typesDirectory, @"*.json", SearchOption.AllDirectories);
            foreach (string schemaFileName in schemaFileNames)
            {
                JObject schema = LoadJsonFile(schemaFileName);
                bool isValid = schema.IsValid(rootSchema);
                if (!isValid)
                {
                    throw new InvalidDataException($"Schema {schemaFileName} is invalid.");
                }

                string url = schema.Properties().First(p => p.Name.Equals("id")).Value.ToString();
                resolver.Add(new Uri(url), schema.ToString());
            }

            return resolver;
        }

        private static JSchema ReadJsonSchemaDraft04()
        {
            // The Json schema draft 04 is an embedded resource of this application.

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "JsonValidator.json-schema.schema";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (StreamReader streamReader = new StreamReader(stream ?? throw new InvalidOperationException("Embeeded resource not found.")))
                {
                    using (JsonTextReader reader = new JsonTextReader(streamReader))
                    {
                        return JSchema.Load(reader);
                    }
                }
            }
        }
    }
}
