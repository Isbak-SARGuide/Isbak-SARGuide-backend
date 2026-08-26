using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Isbak_SAR_Guide.Business.Mapping;

/// <summary>
/// Delta yanitinin zarfini yazar. Zarf ELLE yazilir (fromVersion, toVersion,
/// dizi anahtarlari camelCase, sabit); parcalar (content payload'lari, modul
/// dizisi, eklenen medya) HAM JSON metinleridir - WriteRawValue ile bayt bayt
/// gomulur, hicbir deserialize/re-serialize adimi yoktur. Servisten ayri
/// tutulur ki hem servis sismesin hem bu yazim mantigi tek basina test
/// edilebilsin.
/// </summary>
public static class SyncChangesJsonWriter
{
    // _canonicalOptions'taki encoder ile ayni - zarfin kendisinde kacan
    // karakter olmasa da tutarlilik ve gelecekteki string alanlar icin.
    private static readonly JsonWriterOptions _envelopOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Write(
        int fromVersion,
        int toVersion,
        IReadOnlyList<string> upsertedContentPayloads,
        IReadOnlyList<int> deletedContentIds,
        string modulesRawJson,
        IReadOnlyList<string> addedMediaRawJson,
        IReadOnlyList<int> removedMediaIds)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream, _envelopOptions))
        {
            writer.WriteStartObject();

            writer.WriteNumber("fromVersion", fromVersion);
            writer.WriteNumber("toVersion", toVersion);

            writer.WriteStartArray("upsertedContents");
            foreach (var payload in upsertedContentPayloads)
            {
                writer.WriteRawValue(payload);
            }

            writer.WriteEndArray();

            writer.WriteStartArray("deletedContentIds");
            foreach (var id in deletedContentIds)
            {
                writer.WriteNumberValue(id);
            }

            writer.WriteEndArray();

            writer.WritePropertyName("modules");
            writer.WriteRawValue(modulesRawJson);

            writer.WriteStartArray("addedMedia");
            foreach (var media in addedMediaRawJson)
            {
                writer.WriteRawValue(media);
            }

            writer.WriteEndArray();

            writer.WriteStartArray("removedMediaIds");
            foreach (var id in removedMediaIds)
            {
                writer.WriteNumberValue(id);
            }

            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
