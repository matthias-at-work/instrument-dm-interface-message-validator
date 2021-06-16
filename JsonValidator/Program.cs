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

        //// Prepared settings: (1) Instrument-DM-Interface
        //static readonly string BaseDir = @"C:\Code\GitLab\instrument-dm-interface";
        //static readonly string RelPathToTypesDirectory = @"X800\types";
        //static IEnumerable<string> JsonFileNames = Directory.GetFiles(Path.Combine(BaseDir, "examples"), @"*.json", SearchOption.AllDirectories);

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

        // Prepared settings: (5) Data Upload
        //static readonly string BaseDir = @"C:\Code\GitLab\dm-data-upload\";
        //private static IEnumerable<string> JsonFileNames = new string[]
        //{
        //    @"C:\Code\GitLab\dm-data-upload\examples\compositions\EventsComposition_2019_04_18_16_52_01_343.json",
        //    @"C:\Code\GitLab\dm-data-upload\examples\compositions\InventoryComposition_2019_04_18_15_11_00_018.json",
        //    @"C:\Code\GitLab\dm-data-upload\examples\compositions\ResultsComposition_2019_04_18_15_09_27_321.json",
        //    @"C:\Code\GitLab\dm-data-upload\examples\packages\Package-BBA48610-5427-4A27-95FE-3DC50AA1EDF4.metadata.json",
        //};


        // Prepared settings: (6) Data Export
        static readonly string BaseDir = @"C:\Code\GitLab\dm-data-export\json\";
        private static readonly string RelPathToTypesDirectory = @"X800";
        static IEnumerable<string> JsonFileNames = Directory.GetFiles(Path.Combine(BaseDir, "Examples"), @"*.json", SearchOption.AllDirectories);

        //// Prepared settings: (7) Calculator Interface
        //private static string BaseDir = @"C:\Code\GitLab\calculator-interface";
        //private static readonly string RelPathToTypesDirectory = @"X800\types";
        //private static IEnumerable<string> JsonFileNames = Directory.GetFiles(Path.Combine(BaseDir, "examples"), @"*.json", SearchOption.AllDirectories);



        private static readonly string SchemaDir = Path.Combine(BaseDir, @"schema");
        private static readonly Uri BaseUri = new Uri(@"http://roche.com/rmd/");




        static void Main()
        {
            IList<string> errors = new List<string>();

            // Get a list of all schemas that are in scope.
            IDictionary<Uri, string> schemaDict = GetAllSchema(SchemaDir);
            Console.WriteLine("The following schemas were found:");
            foreach (Uri uri in schemaDict.Keys)
            {
                Console.WriteLine($"  {uri}");
            }

            // Preload a resolver with all these schemas.
            JSchemaPreloadedResolver resolver = new JSchemaPreloadedResolver();
            foreach (KeyValuePair<Uri, string> schema in schemaDict)
            {
                JObject jObject = LoadJsonFile(schema.Value);
                resolver.Add(schema.Key, jObject.ToString());
            }

            // Validate all json-files.
            Console.WriteLine();
            Console.WriteLine("Validating examples");
            foreach (string jsonFileName in JsonFileNames)
            {
                if (!File.Exists(jsonFileName))
                {
                    errors.Add($"[FileNotFound] '{jsonFileName}'");
                    continue;
                }

                JObject jsonObject;
                try
                {
                    jsonObject = LoadJsonFile(jsonFileName);
                }
                catch
                {
                    errors.Add($"[InvalidJsonFile] '{jsonFileName}'");
                    continue;
                }

                // Each json-file has a 'schema' property that indicates the schema it is associated with ('referencedSchema'). 
                string referencedSchema = jsonObject.Value<string>("schema") ?? null;
                if (referencedSchema == null)
                {
                    errors.Add($"[SchemaNotReferenced] File '{jsonFileName}' does not contain a schema-reference ('schema'-property).");
                    continue;
                }
                Uri referencedSchemaUri = new Uri(referencedSchema);

                // Get the schema content.
                string referencedSchemaFile;
                if (!schemaDict.TryGetValue(referencedSchemaUri, out referencedSchemaFile))
                {
                    errors.Add($"[SchemaNotFound] Unable to find schema referenced in file '{jsonFileName}'.");
                    continue;
                }

                // Validate file against schema.
                JSchema schema = LoadSchema(resolver, referencedSchemaFile);
                bool isValid = jsonObject.IsValid(schema, out IList<string> validationErrors);

                Console.WriteLine("  {0} :  {1}", isValid ? "Valid" : "Invalid", jsonFileName);
                if (validationErrors != null)
                {
                    foreach (string validationError in validationErrors)
                    {
                        Console.WriteLine(validationError);
                    }
                }

                if (!isValid)
                {
                    errors.Add($"[ValidationError] Filename: '{jsonFileName}'");
                }
            }

            Console.WriteLine();
            if (errors.Count == 0)
            {
                Console.WriteLine("No errors found!");
            }
            else
            {
                Console.WriteLine("{0} errors found:", errors.Count);
                foreach (string invalidFileName in errors)
                {
                    Console.WriteLine(" - {0}", invalidFileName);
                }
            }

            Console.ReadKey();
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
         
        private static IDictionary<Uri, string> GetAllSchema(string typesDirectory)
        {
            var schemaFiles = new Dictionary<Uri, string>();

            JSchema json04Schema = ReadJsonSchemaDraft04();

            IEnumerable<string> fileNames = Directory.GetFiles(typesDirectory, @"*.json", SearchOption.AllDirectories);
            foreach (string fileName in fileNames)
            {
                JObject jsonObject = LoadJsonFile(fileName);

                // X800 schema files have "$schema" property: Identifies the JSON schema version
                string schema = jsonObject.Value<string>("$schema") ?? null;
                if (string.IsNullOrEmpty(schema))
                {
                    continue;
                }

                bool isValid = jsonObject.IsValid(json04Schema);
                if (!isValid)
                {
                    throw new InvalidDataException($"Schema in file '{fileName}' is invalid (not schema compliant).");
                }

                // X800 schema files have an "id"-property: A unique identifier of the schema (a URI)
                string id = jsonObject.Value<string>("id") ?? null;
                if (string.IsNullOrEmpty(id))
                {
                    throw new InvalidDataException($"Schema in file '{fileName}' lacks the 'id' property.");
                }

                Uri uri = new Uri(id);
                if (schemaFiles.ContainsKey(uri))
                {
                    throw new InvalidDataException($"There exists multiple schema-files with id '{uri.ToString()}'.");
                }

                schemaFiles.Add(uri, fileName);
            }

            return schemaFiles;
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
