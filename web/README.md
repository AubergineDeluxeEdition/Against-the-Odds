# Against the Odds Web

Site statique autonome pour presenter le jeu avec une page immersive, des videos en fond, une presentation des mecaniques et une zone de telechargement.

## Lancer en local

Depuis ce dossier :

```powershell
python -m http.server 8080
```

Puis ouvrir <http://localhost:8080>.

## Servir avec Docker

```bash
cd web
docker compose up -d
```

Le site ecoute sur <http://localhost:8080>.

## Deployer sur le serveur avec Cloudflare Tunnel

Ce compose n'expose aucun port public. Le container rejoint le meme network Docker que `cloudflared`, puis Cloudflare pointe vers `http://against-the-odds-site:8080`.

Premier deploiement avec sparse checkout :

```bash
mkdir -p ~/sites
cd ~/sites

git clone --filter=blob:none --sparse https://github.com/AubergineDeluxeEdition/Against-the-Odds.git against-the-odds-site-repo
cd against-the-odds-site-repo
git sparse-checkout set web

cd web
```

Configurer le deploiement :

```bash
cp .env.example .env
nano .env
```

Trouver le network Docker de `cloudflared` :

```bash
docker inspect cloudflared --format '{{range $name, $_ := .NetworkSettings.Networks}}{{println $name}}{{end}}'
```

Lancer le deploiement :

```bash
chmod +x deploy-pi.sh
./deploy-pi.sh
```

Dans Cloudflare Tunnel, le public hostname doit pointer vers :

```text
HTTP
against-the-odds-site:8080
```

Mise a jour :

```bash
cd ~/sites/against-the-odds-site-repo/web
./deploy-pi.sh
```

Le script fait `git pull`, puis relance le container. Le bouton de telechargement pointe directement vers GitHub Releases.

## Ajouter le build du jeu

1. Exporter le jeu.
2. Publier `Output/AgainstTheOdds-setup.exe` dans la release GitHub `latest-build`.
3. Verifier que le lien dans `index.html` pointe vers `https://github.com/AubergineDeluxeEdition/Against-the-Odds/releases/download/latest-build/AgainstTheOdds-setup.exe`.

## Publier l'installer sur GitHub Release

Depuis la machine de dev, apres avoir exporte `Output/AgainstTheOdds-setup.exe` :

```bash
gh release create latest-build Output/AgainstTheOdds-setup.exe \
  --repo AubergineDeluxeEdition/Against-the-Odds \
  --target main \
  --title "Latest Against the Odds build" \
  --notes "Latest Windows installer."
```

Pour remplacer l'asset apres un nouveau build :

```bash
gh release upload latest-build Output/AgainstTheOdds-setup.exe \
  --repo AubergineDeluxeEdition/Against-the-Odds \
  --clobber
```

Pour que le bouton fonctionne chez les visiteurs sans authentification, la release doit etre accessible publiquement. Une release dans un repository prive reste privee.

## Structure

- `index.html`: contenu de la page.
- `styles.css`: design responsive.
- `assets/`: images et videos embarquees.
- `downloads/`: archives telechargeables.
- `Caddyfile`: serveur statique.
- `docker-compose.yml`: lancement Caddy via Docker.
- `compose.pi.yml`: lancement Caddy derriere un Cloudflare Tunnel Docker.
- `deploy-pi.sh`: pull, puis relance Docker.
