using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AgainstTheOdds.Core
{
    /// <summary>
    /// Données de sauvegarde persistantes. Sérialisé en JSON via JsonUtility.
    /// Les champs doivent rester publics pour être sérialisables par Unity.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public bool isInRun;
        public int currentBossIndex;
        public int potionCount = 0;
        public List<string> runRewardCardIds = new List<string>();
        public List<string> unlockedCards = new List<string>();
        public string selectedDeckId;
        public string lastPlayTimeIso;
    }

    /// <summary>
    /// Gère la lecture/écriture du fichier de sauvegarde dans Application.persistentDataPath/save.json.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private const string NomFichierSauvegarde = "save.json";

        private string CheminFichierSauvegarde => Path.Combine(Application.persistentDataPath, NomFichierSauvegarde);

        /// <summary>Données actuellement en mémoire. Toujours non-null (vide par défaut).</summary>
        public SaveData CurrentData { get; private set; } = new SaveData();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);

            // Chargement auto au démarrage si une save existe
            var donneesChargees = LoadGame();
            if (donneesChargees != null) CurrentData = donneesChargees;
        }

        /// <summary>Écrit les données actuelles dans le fichier de sauvegarde.</summary>
        public void SaveGame()
        {
            CurrentData.lastPlayTimeIso = DateTime.UtcNow.ToString("o"); // ISO 8601

            try
            {
                string json = JsonUtility.ToJson(CurrentData, prettyPrint: true);
                File.WriteAllText(CheminFichierSauvegarde, json);
                Debug.Log($"[SaveManager] Sauvegarde écrite → {CheminFichierSauvegarde}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Échec d'écriture de la sauvegarde : {e.Message}");
            }
        }

        /// <summary>Lit le fichier de sauvegarde. Retourne null si absent ou illisible.</summary>
        public SaveData LoadGame()
        {
            if (!HasSave()) return null;

            try
            {
                string json = File.ReadAllText(CheminFichierSauvegarde);
                SaveData data = JsonUtility.FromJson<SaveData>(json);
                Normalize(data);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Échec de lecture de la sauvegarde : {e.Message}");
                return null;
            }
        }

        public bool HasSave() => File.Exists(CheminFichierSauvegarde);

        public void SaveRun(GameManager gameManager)
        {
            if (gameManager == null) return;

            CurrentData.isInRun = gameManager.IsInRun;
            CurrentData.currentBossIndex = gameManager.CurrentBossIndex;
            CurrentData.potionCount = gameManager.PotionCount;
            CurrentData.selectedDeckId = gameManager.SelectedDeckId;
            CurrentData.runRewardCardIds = new List<string>(gameManager.RunRewardCardIds);
            SaveGame();
        }

        public void SetCurrentData(SaveData data)
        {
            if (data == null) return;

            Normalize(data);
            CurrentData = data;
        }

        private static void Normalize(SaveData data)
        {
            if (data == null) return;

            if (data.runRewardCardIds == null) data.runRewardCardIds = new List<string>();
            if (data.unlockedCards == null) data.unlockedCards = new List<string>();
            if (string.IsNullOrWhiteSpace(data.selectedDeckId))
            {
                data.selectedDeckId = "default";
            }
        }

        public void DeleteSave()
        {
            if (!HasSave())
            {
                CurrentData = new SaveData();
                return;
            }

            try
            {
                File.Delete(CheminFichierSauvegarde);
                CurrentData = new SaveData();
                Debug.Log("[SaveManager] Sauvegarde supprimée.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Échec de suppression de la sauvegarde : {e.Message}");
            }
        }
    }
}
