using UnityEngine;

[CreateAssetMenu(fileName = "CombatCardAudioProfile", menuName = "Against the Odds/Combat/Card Audio Profile")]
public class CombatCardAudioProfile : ScriptableObject
{
    [Header("Fallbacks By Type")]
    public AudioClip attackCardSfx;
    public AudioClip defenseCardSfx;
    public AudioClip utilityCardSfx;
    public AudioClip terrainCardSfx;
    public AudioClip defaultCardSfx;

    [Header("Per Card Overrides")]
    public CardSfxOverride[] cardOverrides;

    public AudioClip GetClip(CardDefinition card)
    {
        if (card == null) return defaultCardSfx;

        if (cardOverrides != null)
        {
            for (int i = 0; i < cardOverrides.Length; i++)
            {
                CardSfxOverride cardOverride = cardOverrides[i];
                if (cardOverride != null && cardOverride.cardId == card.id && cardOverride.sfx != null)
                {
                    return cardOverride.sfx;
                }
            }
        }

        switch (card.type)
        {
            case "attack": return attackCardSfx != null ? attackCardSfx : defaultCardSfx;
            case "defense": return defenseCardSfx != null ? defenseCardSfx : defaultCardSfx;
            case "utility": return utilityCardSfx != null ? utilityCardSfx : defaultCardSfx;
            case "terrain": return terrainCardSfx != null ? terrainCardSfx : defaultCardSfx;
            default: return defaultCardSfx;
        }
    }
}

[System.Serializable]
public class CardSfxOverride
{
    public string cardId;
    public AudioClip sfx;
}
