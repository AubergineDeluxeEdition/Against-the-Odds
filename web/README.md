# Against the Odds Web

Site statique officiel pour **Against the Odds**, un jeu de cartes sombre centré sur les parties hardcore, les combats de boss et la gestion de ressources sous pression.

Le site présente :

- un hero vidéo léger et auto-joué ;
- des sections immersives par zone ;
- les mécaniques principales: brûlure, bouclier, mana, potions ;
- la carte de campagne ;
- une galerie de boss et de cartes ;
- un bouton de téléchargement direct vers la dernière release GitHub.

## Pré-requis public

Le bouton de téléchargement pointe vers :

```text
https://github.com/AubergineDeluxeEdition/Against-the-Odds/releases/latest/download/AgainstTheOdds-setup.exe
```

Pour que ce lien fonctionne chez les visiteurs sans authentification, le repository GitHub doit être public. Une release attachée à un repository privé reste privée.

## Lancer en local

Depuis ce dossier :

```bash
python3 -m http.server 8080
```

Puis ouvrir <http://localhost:8080>.

Alternative Docker locale :

```bash
docker compose up -d
```

Le site écoute sur <http://localhost:8080>.

## Déploiement serveur

Le serveur web est un container Caddy statique. Le compose serveur n’expose aucun port public: le container rejoint le même network Docker que `cloudflared`, puis Cloudflare Tunnel route vers `http://against-the-odds-site:8080`.

Premier déploiement avec sparse checkout :

```bash
git clone --filter=blob:none --sparse git@github.com:AubergineDeluxeEdition/Against-the-Odds.git against-the-odds-site-repo
cd against-the-odds-site-repo
git sparse-checkout set web
cd web
```

Configurer le network Docker de `cloudflared` :

```bash
cp .env.example .env
docker inspect cloudflared --format '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}'
nano .env
```

Lancer :

```bash
bash deploy-pi.sh
```

Dans Cloudflare Tunnel, le public hostname doit pointer vers :

```text
HTTP
against-the-odds-site:8080
```

Le champ `Path` doit rester vide pour servir toutes les routes, assets et téléchargements externes.

## Mise à jour

Depuis le serveur :

```bash
cd /mnt/san/against-the-odds-site-repo/web
bash deploy-pi.sh
```

Le script fait `git pull --ff-only`, puis recrée le container pour forcer Caddy à repartir sur les fichiers à jour. Le téléchargement du jeu ne transite pas par le serveur: le client télécharge directement l’asset GitHub Release.

Vérifier que le container sert bien la nouvelle page :

```bash
docker exec against-the-odds-site grep -n "Gère ta main" /usr/share/caddy/index.html
curl -s https://against-the-odds.amorisetti.ch/?nocache=$(date +%s) | grep "Gère ta main"
```

## Publier un nouveau build

Depuis le Pi, pour pull le dernier `main`, créer une nouvelle release GitHub avec l'installer tracké dans Git, puis redéployer le site :

```bash
cd /mnt/san/against-the-odds-site-repo/web
bash release-and-deploy-pi.sh
```

## Structure

- `index.html`: page statique.
- `styles.css`: design responsive.
- `assets/`: images et vidéos optimisées.
- `Caddyfile`: serveur statique et headers de cache.
- `docker-compose.yml`: lancement local avec port `8080`.
- `compose.pi.yml`: lancement serveur derrière Cloudflare Tunnel.
- `deploy-pi.sh`: pull puis relance Docker.
- `release-and-deploy-pi.sh`: pull, crée une nouvelle release avec `gh`, puis relance Docker.

## Crédits

Against the Odds est réalisé par Alexandre Morisetti, Antoine Dill et Hugo Brinchat.
