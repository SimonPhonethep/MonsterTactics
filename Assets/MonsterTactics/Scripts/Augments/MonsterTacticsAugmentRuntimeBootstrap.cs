using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.2a.2: The Augment Catalog lives in Resources, but older scene installs
    /// could lose the AugmentManager component on a scene reload. Wire it once at
    /// match startup without touching any saved assets or augment definitions.
    /// </summary>
    public static class MonsterTacticsAugmentRuntimeBootstrap
    {
        private const string CatalogResourcePath = "Augments/MonsterTacticsAugmentCatalog";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            // Domain Reload may be disabled. Avoid stacking event subscribers.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeFirstScene()
        {
            string ignored;
            EnsureActiveManager(out ignored);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string ignored;
            EnsureActiveManager(out ignored);
        }

        /// <summary>
        /// Idempotent repair of ONLY the runtime scene component. Existing active
        /// managers are reused, and saved catalog/assets are never edited.
        /// Called by the preview menu too, for immediate diagnosis.
        /// </summary>
        public static MonsterTacticsAugmentManager EnsureActiveManager(out string detail)
        {
            var catalog = Resources.Load<MonsterTacticsAugmentCatalog>(CatalogResourcePath);
            if (catalog == null)
            {
                detail = "Runtime catalog missing from Resources/" + CatalogResourcePath +
                    ". The Balance Editor asset may exist under another path.";
                return null;
            }
            if (!catalog.masterEnabled)
            {
                detail = "Master Enabled is OFF in the saved runtime Augment Catalog.";
                return null;
            }
            var shop = Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (shop == null)
            {
                detail = "No active MonsterTacticsU5ShopManager was found in the playing scene.";
                return null;
            }
            var round = Object.FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            var manager = shop.GetComponent<MonsterTacticsAugmentManager>();

            // Reuse a working manager on a different active object, if any.
            if (manager == null)
            {
                var other = Object.FindAnyObjectByType<MonsterTacticsAugmentManager>();
                if (other != null && other.enabled && other.gameObject.activeInHierarchy)
                {
                    if (other.IsActive || other.TryRecoverStartup(catalog, shop, round))
                    {
                        detail = "Reused existing AugmentManager on " + other.gameObject.name + ".";
                        return other;
                    }
                }
                // Component attaches to the same scene object as the active shop.
                // AddComponent immediately calls Awake and creates the match snapshot.
                manager = shop.gameObject.AddComponent<MonsterTacticsAugmentManager>();
                Debug.LogWarning("[U6.9.2a.2] Augment manager was missing. " +
                    "Restored it at runtime on '" + shop.name + "'. " +
                    "Your catalog and asset settings were NOT changed.", manager);
            }

            if (!manager.enabled) manager.enabled = true;
            if (!manager.IsActive && !manager.TryRecoverStartup(catalog, shop, round))
            {
                detail = "AugmentManager component exists but could not activate. " +
                    "Check whether the manager GameObject is active and the catalog is enabled.";
                return null;
            }
            if (!manager.gameObject.activeInHierarchy)
            {
                detail = "AugmentManager GameObject is inactive. " +
                    "Assign the component to the active Shop Manager GameObject.";
                return null;
            }
            detail = "READY: " + manager.gameObject.name + "; definitions=" +
                manager.DefinitionCount + "; active=" + manager.IsActive + ".";
            return manager;
        }
    }
}
