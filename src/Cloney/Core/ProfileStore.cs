using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Cloney.Core;

internal static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<Profile> Load()
    {
        if (!File.Exists(Paths.ProfilesFile)) return new List<Profile>();
        try
        {
            return JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(Paths.ProfilesFile), Options)
                   ?? new List<Profile>();
        }
        catch (JsonException)
        {
            // Arquivo corrompido: guarda uma cópia antes que um "Salvar" o sobrescreva.
            var backup = Paths.ProfilesFile + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
            File.Copy(Paths.ProfilesFile, backup, overwrite: true);
            return new List<Profile>();
        }
    }

    public static Profile? Find(string id) => Load().FirstOrDefault(p => p.Id == id);

    public static void Save(IEnumerable<Profile> profiles)
    {
        Directory.CreateDirectory(Paths.Root);
        var tmp = Paths.ProfilesFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(profiles.ToList(), Options));
        File.Move(tmp, Paths.ProfilesFile, overwrite: true);
    }
}
