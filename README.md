# Steam Build Tracker pour Playnite
 
Extension [Playnite](https://playnite.link) qui suit les **build ID Steam** de vos jeux, y compris ceux qui ne sont liés à aucun launcher (jeux installés en local , copies hors Steam, 🏴‍☠️). Quand Steam publie un nouveau build, vous recevez une notification dans Playnite : il est temps de mettre le jeu à jour.
 
## Fonctionnalités
 
- **Configurer le suivi** d'un jeu par un simple clic droit.
- **Recherche automatique de l'App ID** Steam à partir du nom du jeu, ou saisie manuelle si la recherche échoue.
- **Choix du build de référence** :
  - automatique : votre version est considérée comme à jour (build en ligne actuel) ;
  - manuel : vous saisissez vous-même le build ID de votre version.
- **Vérification au démarrage** de Playnite de tous les jeux suivis, avec une notification si le build en ligne a changé.
- **Vérification à la demande** depuis le menu du jeu.
- **« Marquer comme à jour »** une fois la mise à jour effectuée.
- **Lien direct vers la page SteamDB** du jeu (patchnotes).
## Installation
 
1. Téléchargez le fichier `.pext` de la dernière version dans l'onglet [**Releases**](../../releases) du dépôt.
2. Fermez Playnite.
3. Double-cliquez sur le fichier `.pext` et confirmez l'installation.
4. Relancez Playnite. L'extension « Steam Build Tracker » apparaît dans **Modules complémentaires → Extensions**.
## Utilisation
 
Faites un clic droit sur un jeu, puis ouvrez le menu **Steam Build Tracker** :
 
| Action | Effet |
|---|---|
| **Configurer le suivi SteamDB** | Lie le jeu à un App ID Steam et définit le build de référence (automatique ou saisi à la main). |
| **Vérifier maintenant** | Compare tout de suite votre build au build en ligne. |
| **Marquer comme à jour (build en ligne actuel)** | Remplace votre build de référence par le build en ligne actuel, après une mise à jour. |
| **Ouvrir sur SteamDB** | Ouvre la page de patchnotes du jeu sur SteamDB dans votre navigateur. |
 
## Comment ça marche
 
- La recherche d'App ID passe par l'API de recherche publique du magasin Steam.
- Le build ID de la branche `public` est récupéré via le service tiers [`api.steamcmd.net`](https://api.steamcmd.net), qui expose les informations publiques de Steam.
- Les jeux suivis sont enregistrés dans un fichier `tracked.json`, dans le dossier de données de l'extension (géré par Playnite).
- **SteamDB n'est pas interrogé automatiquement** : le site n'a pas d'API publique et son scraping n'est pas autorisé. Le lien du menu ouvre simplement la page dans votre navigateur.
## Limites connues
 
- Un jeu hors Steam ne contient aucune information de version : le build « local » est la valeur de référence que **vous** définissez.
- Seule la branche `public` est suivie pour le moment.
- La vérification automatique se fait au démarrage de Playnite.
- L'extension dépend de la disponibilité de `api.steamcmd.net` ; en cas d'échec, vous pouvez saisir le build ID manuellement.
- Si vous sélectionnez plusieurs jeux, seul le premier est pris en compte dans le menu.

## Licence
 
Voir le fichier [LICENSE.txt](LICENSE.txt).
