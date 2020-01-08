using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace JsonValidator
{
    static class Program
    {
        // Set the path to the repo here!

        // Prepared settings: (1) Instrument-DM-Interface
        static readonly string BaseDir = @"C:\Code\GitLab\instrument-dm-interface";
        static readonly string RelPathToTypesDirectory = @"X800\types";
        static IEnumerable<string> JsonFileNames = Directory.GetFiles(Path.Combine(BaseDir, "examples"), @"*.json", SearchOption.AllDirectories);

        //// Prepared settings: (2) ASAP
        //static readonly string BaseDir = @"C:\Code\GitLab\ASAP";
        //static readonly string RelPathToTypesDirectory = @"X800\asap\types";
        //static IEnumerable<string> JsonFileNames = Directory.GetFiles(Path.Combine(BaseDir, "examples"), @"*.json", SearchOption.AllDirectories);

        //// Prepared settings: (3) key-value-translations
        //static readonly string BaseDir = @"C:\Code\GitLab\coded-values-audit-records";
        //static readonly string RelPathToTypesDirectory = null; // no shared types
        //static IEnumerable<string> JsonFileNames = new[]
        //{
        //    Path.Combine(BaseDir, "AuditRecordEventCodes.json"),
        ////    Path.Combine(BaseDir, "Flags.json"),
        // //   Path.Combine(BaseDir, "SampleTypes.json")
        //};

        //// Prepared settings: (4) Access control API
        //static readonly string BaseDir = @"C:\Code\GitLab\dm-access-control-api\jwt";
        //private static readonly string RelPathToTypesDirectory = null;
        //static IEnumerable<string> JsonFileNames = Directory.GetFiles(Path.Combine(BaseDir, "examples"), @"*.json", SearchOption.AllDirectories);

        //// Prepared settings: (5) Data Upload
        //static readonly string BaseDir = @"C:\Code\GitLab\dm-data-upload\";
        //private static readonly string RelPathToTypesDirectory = @"x800\dm";
        //static IEnumerable<string> JsonFileNames = Directory.GetFiles(Path.Combine(BaseDir, "examples"), @"*.json", SearchOption.AllDirectories);

        //private static IEnumerable<string> JsonFileNames = new string[]
        //{
        //    @"C:\Code\GitLab\dm-data-upload\Example\Package-BBA48610-5427-4A27-95FE-3DC50AA1EDF4.metadata.json",
        //    @"C:\Code\GitLab\dm-data-upload\Example\Bundle 1\Settings_2019_04_18_15_08_32_500.json",
        //    @"C:\Code\GitLab\dm-data-upload\Example\Bundle 2\Inventory_2019_04_18_15_11_00_018.json",
        //};


        private static readonly string SchemaDir = Path.Combine(BaseDir, @"schema");
        private static readonly Uri BaseUri = new Uri(@"http://roche.com/rmd/");

        static void Main()
        {

            // create a resolver with all type-schemas loaded.
            JSchemaPreloadedResolver resolver = new JSchemaPreloadedResolver();
            if (!string.IsNullOrEmpty(RelPathToTypesDirectory))
            {
                AddTypesToResolver(resolver, Path.Combine(SchemaDir, RelPathToTypesDirectory));
            }

            Console.WriteLine("SchemaResolver was initialized with the following types:");
            foreach (Uri uri in resolver.PreloadedUris)
            {
                Console.WriteLine("  {0}", uri);
            }

            // load all examples and validate them.
            Console.WriteLine();
            Console.WriteLine("Validating examples");

            IList<string> invalidFileNames = new List<string>();
            foreach (string jsonMessageFileName in JsonFileNames)
            {
                JObject jsonMessage = LoadJsonFile(jsonMessageFileName);
                Uri referencedMessageSchema = GetReferencedSchema(jsonMessage);

                JSchema messageSchema = LoadMessageSchema(referencedMessageSchema, resolver);
                bool isValid = jsonMessage.IsValid(messageSchema, out IList<string> errors);

                Console.WriteLine("  {0} :  {1}", isValid ? "Valid" : "Invalid", jsonMessageFileName);
                if (errors != null)
                {
                    foreach (string error in errors)
                    {
                        Console.WriteLine(error);
                    }
                }

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

        private static void AddTypesToResolver(JSchemaPreloadedResolver resolver, string typesDirectory)
        {
            JSchema rootSchema = ReadJsonSchemaDraft04();

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
