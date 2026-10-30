using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace SteamBuildTracker
{
    public class TrackedGame
    {
        public int AppId { get; set; }
        public string Branch { get; set; } = "public";
        public string LocalBuildId { get; set; }
        public string LastKnownOnlineBuildId { get; set; }
    }

    public class SteamBuildTracker : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly HttpClient http = new HttpClient();
        private readonly string dataFile;
        private Dictionary<Guid, TrackedGame> tracked = new Dictionary<Guid, TrackedGame>();

        public override Guid Id { get; } = Guid.Parse("BB888004-67EC-4046-99C3-F239AB656536");

        public SteamBuildTracker(IPlayniteAPI api) : base(api)
        {
            dataFile = Path.Combine(GetPluginUserDataPath(), "tracked.json");
            if (File.Exists(dataFile))
                tracked = JsonConvert.DeserializeObject<Dictionary<Guid, TrackedGame>>(File.ReadAllText(dataFile))
                          ?? tracked;
        }

        private void Save() => File.WriteAllText(dataFile, JsonConvert.SerializeObject(tracked, Newtonsoft.Json.Formatting.Indented));

        // menu clic drroit
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var game = args.Games.First();
            const string section = "Steam Build Tracker";

            yield return new GameMenuItem
            {
                Description = "Configurer le suivi SteamDB",
                MenuSection = section,
                Action = async _ => await ConfigureGameAsync(game)
            };
            yield return new GameMenuItem
            {
                Description = "Vérifier maintenant",
                MenuSection = section,
                Action = async _ => await CheckGameAsync(game, true)
            };
            yield return new GameMenuItem
            {
                Description = "Marquer comme à jour (build en ligne actuel)",
                MenuSection = section,
                Action = async _ => await MarkUpToDateAsync(game)
            };
            yield return new GameMenuItem
            {
                Description = "Ouvrir sur SteamDB",
                MenuSection = section,
                Action = _ =>
                {
                    if (tracked.TryGetValue(game.Id, out var t))
                        System.Diagnostics.Process.Start($"https://steamdb.info/app/{t.AppId}/patchnotes/");
                    else
                        PlayniteApi.Dialogs.ShowMessage("Ce jeu n'est pas encore configuré.");
                }
            };
        }

        // Liaison
        private async Task ConfigureGameAsync(Game game)
        {
            // App ID ---
            int appId;
            string branch = "public";

            if (tracked.TryGetValue(game.Id, out var existing))
            {
                // Déjà configuré ? alors on garde les infos deja enregistrées ou pas
                var keep = PlayniteApi.Dialogs.ShowMessage(
                    $"Ce jeu est déjà lié à l'App ID {existing.AppId} (build local : {existing.LocalBuildId}).\n\n" +
                    "Oui = garder cet App ID et changer seulement le build\n" +
                    "Non = choisir un autre App ID",
                    "Configurer le suivi", System.Windows.MessageBoxButton.YesNoCancel);

                if (keep == System.Windows.MessageBoxResult.Cancel) return;

                if (keep == System.Windows.MessageBoxResult.Yes)
                {
                    appId = existing.AppId;
                    branch = existing.Branch;
                }
                else
                {
                    var newId = await PickAppIdAsync(game);
                    if (newId == null) return;
                    appId = newId.Value;
                }
            }
            else
            {
                var newId = await PickAppIdAsync(game);
                if (newId == null) return;
                appId = newId.Value;
            }

            // build en ligne
            string onlineBuild = null;
            try { onlineBuild = await GetOnlineBuildAsync(appId, branch); }
            catch (Exception ex) { logger.Error(ex, "Impossible de récupérer le build en ligne"); }

            // choix auto ou à la mano
            string localBuild = null;

            if (onlineBuild != null)
            {
                var choice = PlayniteApi.Dialogs.ShowMessage(
                    $"Build en ligne actuel (branche « {branch} ») : {onlineBuild}\n\n" +
                    "Oui = considérer ma version comme à jour (utiliser ce build)\n" +
                    "Non = saisir moi-même le build ID de ma version\n" +
                    "Annuler = abandonner",
                    "Build ID", System.Windows.MessageBoxButton.YesNoCancel);

                if (choice == System.Windows.MessageBoxResult.Cancel) return;
                if (choice == System.Windows.MessageBoxResult.Yes) localBuild = onlineBuild;
            }
            else
            {
                PlayniteApi.Dialogs.ShowMessage(
                    "Le build en ligne n'a pas pu être récupéré. Vous pouvez saisir le build ID manuellement.");
            }

            if (localBuild == null)
            {
                var input = PlayniteApi.Dialogs.SelectString(
                    "Saisissez le build ID de votre version (chiffres uniquement).\n" +
                    "Vous le trouvez sur SteamDB, page « Patches » du jeu.",
                    "Build ID manuel",
                    existing?.LocalBuildId ?? "");

                if (!input.Result) return;

                var text = input.SelectedString?.Trim();
                if (string.IsNullOrEmpty(text) || !text.All(char.IsDigit))
                {
                    PlayniteApi.Dialogs.ShowErrorMessage("Le build ID doit contenir uniquement des chiffres.", "Build ID invalide");
                    return;
                }
                localBuild = text;
            }

            // c'est l'enregsitrement en gros
            tracked[game.Id] = new TrackedGame
            {
                AppId = appId,
                Branch = branch,
                LocalBuildId = localBuild,
                LastKnownOnlineBuildId = onlineBuild
            };
            Save();

            PlayniteApi.Dialogs.ShowMessage(
                $"Suivi configuré pour « {game.Name} ».\nApp ID : {appId}\nBuild local : {localBuild}" +
                (onlineBuild != null ? $"\nBuild en ligne : {onlineBuild}" : ""));

            // Si le build saisi est différent du build en ligne, prévenir pour pas attendre comme un looser
            if (onlineBuild != null && onlineBuild != localBuild)
                await CheckGameAsync(game, false);
        }

        // Retourne l'App ID choisi, ou null si l'utilisateur annule
        private async Task<int?> PickAppIdAsync(Game game)
        {
            List<SteamSearchItem> results = null;
            try { results = await SearchSteamAsync(game.Name); }
            catch (Exception ex) { logger.Error(ex, "Recherche Steam échouée"); }

            if (results != null && results.Any())
            {
                var choices = results.Select(r => new GenericItemOption(r.Name, r.Id.ToString())).ToList();
                var picked = PlayniteApi.Dialogs.ChooseItemWithSearch(
                    choices,
                    q => choices.Where(c => c.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList(),
                    game.Name,
                    "Choisir le jeu Steam");

                if (picked != null) return int.Parse(picked.Description);
                return null;
            }

            // Aucun résultat : hop hop hop à la mano
            var input = PlayniteApi.Dialogs.SelectString(
                "Aucun résultat sur Steam. Saisissez l'App ID manuellement\n" +
                "(le nombre dans l'URL steamdb.info/app/XXXXXX).",
                "App ID manuel", "");

            if (!input.Result) return null;
            if (int.TryParse(input.SelectedString?.Trim(), out var id)) return id;

            PlayniteApi.Dialogs.ShowErrorMessage("App ID invalide.", "Erreur");
            return null;
        }

        private async Task MarkUpToDateAsync(Game game)
        {
            if (!tracked.TryGetValue(game.Id, out var t)) return;
            t.LocalBuildId = await GetOnlineBuildAsync(t.AppId, t.Branch);
            t.LastKnownOnlineBuildId = t.LocalBuildId;
            Save();
        }

        // Vérification
        public override async void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Supprime les suivis dont le jeu n'existe plus
            var orphans = tracked.Keys.Where(id => PlayniteApi.Database.Games.Get(id) == null).ToList();
            if (orphans.Any())
            {
                foreach (var id in orphans) tracked.Remove(id);
                Save();
            }

            foreach (var kv in tracked.ToList())
            {
                var game = PlayniteApi.Database.Games.Get(kv.Key);
                if (game != null) await CheckGameAsync(game, false);
                await Task.Delay(500); // ne pas spammer
            }
        }

        private async Task CheckGameAsync(Game game, bool verbose)
        {
            if (!tracked.TryGetValue(game.Id, out var t)) return;
            try
            {
                var online = await GetOnlineBuildAsync(t.AppId, t.Branch);
                t.LastKnownOnlineBuildId = online;
                Save();

                if (online != t.LocalBuildId)
                {
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"buildtracker_{game.Id}_{online}",
                        $"{game.Name} : nouveau build {online} (le vôtre : {t.LocalBuildId}). Mise à jour requise.",
                        NotificationType.Info));
                }
                else if (verbose)
                {
                    PlayniteApi.Dialogs.ShowMessage($"{game.Name} est à jour (build {online}).");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Vérification échouée pour {game.Name}");
            }
        }

        // merci steam
        private class SteamSearchItem { public int Id; public string Name; }

        private async Task<List<SteamSearchItem>> SearchSteamAsync(string term)
        {
            var url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(term)}&cc=fr&l=french";
            var json = JObject.Parse(await http.GetStringAsync(url));
            return json["items"].Select(i => new SteamSearchItem
            {
                Id = (int)i["id"],
                Name = (string)i["name"]
            }).ToList();
        }

        // Version simple via API steam encore merci gabe le goat
        private async Task<string> GetOnlineBuildAsync(int appId, string branch)
        {
            var json = JObject.Parse(await http.GetStringAsync($"https://api.steamcmd.net/v1/info/{appId}"));
            return (string)json["data"][appId.ToString()]["depots"]["branches"][branch]["buildid"];
        }
    }
}