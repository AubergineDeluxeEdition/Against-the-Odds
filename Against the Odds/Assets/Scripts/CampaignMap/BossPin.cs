using UnityEngine;

namespace AgainstTheOdds.CampaignMap
{
    /// <summary>
    /// À attacher sur chaque GameObject "pin" placé manuellement sur les cartes de la scène 03_CampaignMap.
    /// S'enregistre dans CampaignPinRegistry pour être retrouvable par son bossIndex.
    /// </summary>
    public class BossPin : MonoBehaviour
    {
        [Header("Progression")]
        [Tooltip("Index du boss représenté par ce pin (0 = premier boss, 9 = boss final). Doit être unique dans la scène.")]
        [SerializeField] private int bossIndex = 0;

        public int BossIndex => bossIndex;

        // OnEnable / OnDisable plutôt que Start / OnDestroy : la registration reste correcte
        // si le pin est désactivé/réactivé à chaud, et elle est garantie d'avoir eu lieu
        // avant le premier Update (donc avant le Start de la caméra qui interroge le registre).
        private void OnEnable()
        {
            CampaignPinRegistry.Register(this);
        }

        private void OnDisable()
        {
            CampaignPinRegistry.Unregister(this);
        }
    }
}
