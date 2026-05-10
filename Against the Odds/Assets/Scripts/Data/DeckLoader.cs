using UnityEngine;

public static class DeckLoader
{
    // Placer Deck.json dans Assets/Resources/Data/Deck.json
    public static DeckDefinition Load(string resourcePath)
    {
        TextAsset json = Resources.Load<TextAsset>(resourcePath);
        if (json == null)
        {
            Debug.LogError($"[DeckLoader] Fichier introuvable : Resources/{resourcePath}");
            return null;
        }
        DeckDefinition deck = JsonUtility.FromJson<DeckDefinition>(json.text);
        if (deck == null || deck.deck == null || deck.cards == null)
        {
            Debug.LogError($"[DeckLoader] Deck invalide : Resources/{resourcePath}");
            return null;
        }

        return deck;
    }
}
