using System.Collections.Generic;
using UnityEngine;

namespace AgainstTheOdds.CampaignMap
{
    /// <summary>
    /// Registre statique des BossPin présents dans la scène 03_CampaignMap.
    /// Chaque BossPin s'enregistre lui-même à l'activation ; la caméra (ou tout autre système)
    /// retrouve un pin par son bossIndex via GetPin().
    /// </summary>
    public static class CampaignPinRegistry
    {
        private static readonly Dictionary<int, BossPin> pinsParIndex = new Dictionary<int, BossPin>();
        private static readonly List<BossPin> pinsBuffer = new List<BossPin>();

        // Unity conserve l'état statique entre deux Play si "Domain Reload" est désactivé : on force le reset.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            pinsParIndex.Clear();
        }

        public static void Register(BossPin pin)
        {
            if (pin == null) return;

            int index = pin.BossIndex;
            if (pinsParIndex.TryGetValue(index, out var existant) && existant != pin)
            {
                Debug.LogWarning($"[CampaignPinRegistry] Deux BossPin pour l'index {index} : '{existant.name}' écrasé par '{pin.name}'.");
            }
            pinsParIndex[index] = pin;
        }

        public static void Unregister(BossPin pin)
        {
            if (pin == null) return;

            int index = pin.BossIndex;
            if (pinsParIndex.TryGetValue(index, out var existant) && existant == pin)
            {
                pinsParIndex.Remove(index);
            }
        }

        /// <summary>Retourne le pin correspondant à l'index donné, ou null s'il n'existe pas.</summary>
        public static BossPin GetPin(int bossIndex)
        {
            pinsParIndex.TryGetValue(bossIndex, out var pin);
            return pin;
        }

        public static IReadOnlyList<BossPin> GetAllPins()
        {
            pinsBuffer.Clear();
            foreach (BossPin pin in pinsParIndex.Values)
            {
                if (pin != null && pin.IsVisibleOnCampaign)
                {
                    pinsBuffer.Add(pin);
                }
            }

            pinsBuffer.Sort((a, b) => a.BossIndex.CompareTo(b.BossIndex));
            return pinsBuffer;
        }
    }
}
