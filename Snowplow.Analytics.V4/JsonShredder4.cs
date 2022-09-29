/*
 * JsonShredder.cs
 * Copyright (c) 2017 Snowplow Analytics Ltd. All rights reserved.
 * This program is licensed to you under the Apache License Version 2.0,
 * and you may not use this file except in compliance with the Apache License
 * Version 2.0. You may obtain a copy of the Apache License Version 2.0 at
 * http://www.apache.org/licenses/LICENSE-2.0.
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the Apache License Version 2.0 is distributed on
 * an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the Apache License Version 2.0 for the specific
 * language governing permissions and limitations there under.
 * Authors: Devesh Shetty
 * Copyright: Copyright (c) 2017 Snowplow Analytics Ltd
 * License: Apache License Version 2.0
 */

using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Snowplow.Analytics.V4;

/// <summary>
/// Converts unstructured events and custom contexts to a format which the Elasticsearch
/// mapper can understand
/// </summary>
public static class JsonShredder4
{
    public static void WriteContexts(Utf8JsonWriter writer, ReadOnlySpan<char> token)
    {
        if (token.Length > 0)
        {
            ////ReadOnlySpan<byte> bytes = MemoryMarshal.Cast<char, byte>(token);
            Span<byte> bytes = stackalloc byte[token.Length];
            Encoding.UTF8.GetBytes(token, bytes);
            var json = ParseContexts(bytes);
            writer.WriteRawValue(json, true);
        }
    }

    public static void WriteUnstruct(Utf8JsonWriter writer, ReadOnlySpan<char> token)
    {
        if (token.Length > 0)
        {
            ReadOnlySpan<byte> bytes = MemoryMarshal.Cast<char, byte>(token);
            var json = ParseUnstruct(bytes);
            writer.WriteRawValue(json, true);
        }
    }

    public static ReadOnlySpan<char> ParseContexts(ReadOnlySpan<byte> contexts)
    {
        Utf8JsonReader reader = new Utf8JsonReader(contexts);

        reader = ReadPreamble(reader);

        while (true)
        {
            reader = ReadDataBlock(reader, out bool done);

            if (done)
            {
                break;
            }
        }

        ReadEnd(reader);

        return string.Empty;
    }

    private static Utf8JsonReader ReadEnd(Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new Exception();
        }

        if (!reader.Read())
        {
            throw new Exception();
        }

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new Exception();
        }

        bool more = reader.Read();

        if (more)
        {
            throw new Exception();
        }

        return reader;
    }

    private static Utf8JsonReader ReadDataBlock(Utf8JsonReader reader, out bool done)
    {
        reader = ReadDataBlockPreamble(reader, out done);

        if (done)
        {
            return reader;
        }

        reader = ReadDataBlockProperties(reader);

        reader.Read();

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new Exception();
        }

        return reader;
    }

    private static Utf8JsonReader ReadDataBlockProperties(Utf8JsonReader reader)
    {
        while (true)
        {
            reader.Read();

            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new Exception();
            }

            string key = reader.GetString();
            reader.Read();

            if (reader.TokenType == JsonTokenType.String)
            {
                string value = reader.GetString();
                Console.WriteLine($"{key} : {value}");
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt64(out long l))
                {
                    Console.WriteLine($"{key} : {l}");
                }
                else if (reader.TryGetDouble(out double d))
                {
                    Console.WriteLine($"{key} : {d}");
                }
                else
                {
                    throw new Exception();
                }
            }
            else if (reader.TokenType == JsonTokenType.False)
            {
                bool value = reader.GetBoolean();
                Console.WriteLine($"{key} : {value}");
            }
            else if (reader.TokenType == JsonTokenType.True)
            {
                bool value = reader.GetBoolean();
                Console.WriteLine($"{key} : {value}");
            }
            else
            {
                throw new Exception();
            }
        }

        return reader;
    }

    private static Utf8JsonReader ReadDataBlockPreamble(Utf8JsonReader reader, out bool done)
    {
        reader.Read();

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            done = true;

            if (reader.TokenType != JsonTokenType.EndArray)
            {
                throw new Exception();
            }

            return reader;
        }

        reader.Read();

        string text = reader.GetString();

        if (text != "schema")
        {
            throw new Exception();
        }

        reader.Read();

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new Exception();
        }

        text = reader.GetString();

        Console.Write("Schema: ");
        Console.WriteLine(text);
        reader.Read();

        text = reader.GetString();

        if (text != "data")
        {
            throw new Exception();
        }

        reader.Read();

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new Exception();
        }

        done = false;
        return reader;
    }

    private static Utf8JsonReader ReadPreamble(Utf8JsonReader reader)
    {
        reader.Read();

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new Exception();
        }

        reader.Read();

        if (reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new Exception();
        }

        string? text = reader.GetString();

        if (text != "schema")
        {
            throw new Exception();
        }

        reader.Read();

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new Exception();
        }

        string schema = reader.GetString();

        if (schema != "iglu:com.snowplowanalytics.snowplow/contexts/jsonschema/1-0-1")
        {
            throw new Exception();
        }

        reader.Read();

        if (reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new Exception();
        }

        text = reader.GetString();

        if (text != "data")
        {
            throw new Exception();
        }

        reader.Read();

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new Exception();
        }

        return reader;
    }

    public static ReadOnlySpan<char> ParseUnstruct(ReadOnlySpan<byte> token)
    {
        throw new NotImplementedException();
    }

    //Canonical Iglu Schema URI regex
    private static readonly string SCHEMA_PATTERN = @"^iglu:" +                      // Protocol
                                                    @"([a-zA-Z0-9-_.]+)/" +            // Vendor
                                                    @"([a-zA-Z0-9-_]+)/" +             // Name
                                                    @"([a-zA-Z0-9-_]+)/" +             // Format
                                                    @"([1-9][0-9]*" +                  // MODEL (cannot start with 0)
                                                    @"(?:-(?:0|[1-9][0-9]*)){2})$";    // REVISION and ADDITION
    // Extract whole SchemaVer within single group


    /// <summary>
    /// Extracts the vendor, name, format and version from Iglu uri.
    /// </summary>
    /// <param name="uri">Iglu URI.</param>
    /// <returns>The schema information.</returns>
    /// <exception cref="SnowplowEventTransformationException">Thrown when it is not a valid uri</exception>
    private static Dictionary<string, string> ExtractSchema(string uri)
    {
        Match match = Regex.Match(uri, SCHEMA_PATTERN);

        if (match.Success)
        {
            var groups = match.Groups;

            return new Dictionary<string, string>(){
                {"vendor", groups[1].Value},
                {"name", groups[2].Value},
                {"format", groups[3].Value},
                {"version", groups[4].Value}
            };
        }
        else
        {
            throw new SnowplowEventTransformationException4($"Schema {uri} does not conform to schema pattern.");
        }

    }


    /// <summary>
    /// Transform Iglu URI into elasticsearch-compatible column name
    /// </summary>
    /// <example>
    /// "iglu:com.acme/PascalCase/jsonschema/13-0-0" -> "context_com_acme_pascal_case_13"
    /// </example>
    /// <param name="prefix">"context" or "unstruct_event".</param>
    /// <param name="igluUri">Schema field from an incoming JSON, should be already validated</param>
    /// <returns>Elasticsearch field name</returns>
    public static string FixSchema(string prefix, string igluUri)
    {
        var schemaDict = ExtractSchema(igluUri);

        // Split the vendor's reversed domain name using underscores rather than dots
        var snakeCaseOrganization = Regex.Replace(schemaDict["vendor"], @"[\.\-]", @"_").ToLower();

        // Change the name from PascalCase or lisp-case to snake_case if necessary
        var snakeCaseName = Regex.Replace(schemaDict["name"], @"[\.\-]", @"_");
        snakeCaseName = Regex.Replace(snakeCaseName, @"([^A-Z_])([A-Z])",
            (match) => match.Groups[1].Value + "_" + match.Groups[2].Value).ToLower();

        // Extract the schemaver version's model
        var model = schemaDict["version"].Split('-')[0];

        return $"{prefix}_{snakeCaseOrganization}_{snakeCaseName}_{model}";
    }
}
