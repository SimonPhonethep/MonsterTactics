using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.7.2 TFT-inspired PvE loot. Builds a constrained per-match reward plan ONCE,
    /// rather than rolling independently each time an opponent dies.
    /// This revision makes loot feel closer to TFT:
    /// - rarer balls are larger
    /// - balls launch from defeated PvE enemies
    /// - gold is hidden inside the ball until pickup
    /// - opening a gold ball scatters collectible coins on the board
    /// - each coin grants exactly 1 gold only when the trainer collects it
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsLootController : MonoBehaviour
    {
        public enum Tier { Silver, Blue, Gold, Prismatic }

        public struct Award
        {
            public Tier Tier;
            public bool IsComponent;
            public bool IsCapsule;
            // U6.11.8: unit reward is decided at BALL SPAWN, not on pickup.
            public bool IsMonster;
            public string MonsterFamily;
            public MonsterTacticsItemCapsuleKind CapsuleKind;
            public MonsterTacticsItemKind Component;
            public int Coins;
            public bool HasBonus;
            public MonsterTacticsItemKind BonusComponent;
            public override string ToString() => Tier + ": " +
                (IsMonster ? "+1 " + MonsterFamily + " (Monster)" :
                IsCapsule ? MonsterTacticsItemCapsules.DisplayName(CapsuleKind) :
                IsComponent ? MonsterTacticsItemTypes.Name(Component) +
                    (HasBonus ? " + " + MonsterTacticsItemTypes.Name(BonusComponent) : "") : Coins + " gold");
        }

        private sealed class Pickup
        {
            public Award Reward;
            public Transform Root;
            public Transform Ball;
            public SpriteRenderer Aura;
            public Vector3 Start;
            public Vector3 End;
            public float Phase;
            public float SpawnTime;
            public float LaunchDuration;
            public float BounceHeight;
            public bool Settled;
            public float BaseScale;
        }

        private sealed class CoinPickup
        {
            public Transform Root;
            public SpriteRenderer Visual;
            public Vector3 Start;
            public Vector3 End;
            public float SpawnTime;
            public float TravelDuration;
            public float Phase;
            public bool Settled;
        }

        [Header("TFT-style match-level loot fairness")]
        [SerializeField] private int earlyGuaranteedComponents = 4;
        [SerializeField, Range(0f, 1f)] private float prismaticMatchChance = .02f;
        [SerializeField] private float pickupRadius = .92f;
        [SerializeField] private float ballWorldSize = .90f;
        [SerializeField] private float coinWorldSize = .54f;
        [SerializeField] private float coinPickupRadius = .47f;
        [SerializeField] private float launchDuration = .58f;
        [SerializeField] private float launchBounceHeight = .90f;
        [Header("U6.11.9 - OPTIONAL PvE Monster balls (extra loot, not replacements)")]
        [SerializeField] private bool enableAutoPveMonsterBalls = false;
        [SerializeField, Range(0f, 1f)] private float regularPveMonsterChance = .25f;
        [SerializeField, Range(0f, 1f)] private float bossPveMonsterChance = .50f;
        [SerializeField] private bool includeStageOneMonsterBalls = false;
        [SerializeField] private bool allowLateMasterMonsterBalls = false;
        private int autoPveMonsterBallsSpawned;

        public bool AutoPveMonsterBallsEnabled => enableAutoPveMonsterBalls;
        public float RegularPveMonsterChance => regularPveMonsterChance;
        public float BossPveMonsterChance => bossPveMonsterChance;
        public bool IncludeStageOneMonsterBalls => includeStageOneMonsterBalls;
        public bool AllowLateMasterMonsterBalls => allowLateMasterMonsterBalls;
        public int AutoPveMonsterBallsSpawned => autoPveMonsterBallsSpawned;

        /// <summary>
        /// Test-window override for THIS Play Mode session. Settings can also be
        /// edited on the LootController in the Unity Inspector before Play Mode.
        /// This does not change the match's existing item/gold loot plan.
        /// </summary>
        public void ConfigureAutoPveMonsterBalls(bool enabled, float regularChance,
            float bossChance, bool stageOne, bool allowMaster)
        {
            enableAutoPveMonsterBalls = enabled;
            regularPveMonsterChance = Mathf.Clamp01(regularChance);
            bossPveMonsterChance = Mathf.Clamp01(bossChance);
            includeStageOneMonsterBalls = stageOne;
            allowLateMasterMonsterBalls = allowMaster;
        }
        private readonly Dictionary<string, List<Award>> plan = new Dictionary<string, List<Award>>();
        private readonly HashSet<string> releasedEncounters = new HashSet<string>();
        private readonly List<Pickup> active = new List<Pickup>();
        private readonly List<CoinPickup> looseCoins = new List<CoinPickup>();
        private readonly Dictionary<Tier, Sprite> balls = new Dictionary<Tier, Sprite>();
        private readonly Dictionary<Tier, Sprite> glows = new Dictionary<Tier, Sprite>();
        private MonsterTacticsItemInventory inventory;
        private MonsterTacticsU5ShopManager shop;
        private TrainerMovement trainer;
        private Camera cam;
        private bool prepared;
        private int seed;
        private string lastPickup = "";
        private float pickupNoticeUntil;
        private GUIStyle noticeStyle;
        private Sprite coinSprite;
        private readonly Dictionary<MonsterTacticsItemCapsuleKind, Sprite> capsuleSprites =
            new Dictionary<MonsterTacticsItemCapsuleKind, Sprite>();
        private float nextFullNoticeTime;

        public int PickupCount => active.Count;
        public int LooseCoinCount => looseCoins.Count;
        public int MatchSeed => seed;
        public bool MasterBallScheduled { get; private set; }
        public int PlannedComponents
        {
            get
            {
                int n = 0;
                foreach (var list in plan.Values)
                    foreach (Award a in list)
                        if (a.IsComponent) n += a.HasBonus ? 2 : 1;
                return n;
            }
        }

        private static string Key(int stage, int round) => stage + "-" + round;

        private void Awake()
        {
            shop = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            inventory = shop != null ? shop.GetComponent<MonsterTacticsItemInventory>() : null;
            cam = Camera.main;
        }

        private void Start() { PreparePlan(); }

        private void PreparePlan()
        {
            if (prepared) return;
            prepared = true;
            seed = UnityEngine.Random.Range(1, int.MaxValue);
            System.Random rng = new System.Random(seed);

            var stage1Tiers = new List<Tier> { Tier.Silver, Tier.Silver, Tier.Silver, Tier.Silver, Tier.Blue, Tier.Blue };
            Shuffle(stage1Tiers, rng);
            var componentBag = new List<MonsterTacticsItemKind>(MonsterTacticsItemTypes.Components);
            Shuffle(componentBag, rng);
            int cursor = 0, bagCursor = 0;
            int[] slots = { 1, 2, 1, 2 };
            for (int round = 1; round <= 4; round++)
            {
                var rewards = new List<Award>();
                for (int i = 0; i < slots[round - 1]; i++)
                {
                    Tier tier = stage1Tiers[cursor++];
                    bool guaranteedComponent = i == 0;
                    Award reward = guaranteedComponent
                        ? ComponentAward(tier, componentBag[bagCursor++])
                        : CoinAward(tier, 1 + rng.Next(1, 4));
                    rewards.Add(reward);
                }
                plan[Key(1, round)] = rewards;
            }

            if (earlyGuaranteedComponents > 4)
            {
                for (int n = 4; n < Mathf.Min(earlyGuaranteedComponents, 6); n++)
                {
                    var rewards = plan[Key(1, n == 4 ? 4 : 2)];
                    int last = rewards.Count - 1;
                    rewards[last] = ComponentAward(rewards[last].Tier, NextComponent(rng, componentBag, ref bagCursor));
                }
            }

            MasterBallScheduled = rng.NextDouble() < prismaticMatchChance;
            int rareStage = MasterBallScheduled ? rng.Next(5, 8) : -1;
            for (int stage = 2; stage <= 8; stage++)
            {
                int number = stage == 8 ? 1 : 7;
                bool late = stage >= 5;
                int count = late ? 3 : 2;
                var rewards = new List<Award>();
                for (int i = 0; i < count; i++)
                {
                    Tier tier = ChooseTier(rng, late);
                    if (stage == rareStage && i == count - 1) tier = Tier.Prismatic;
                    // Tools appear only in later non-guaranteed reward slots.
                    // This cannot replace the guaranteed components in slots 0/1.
                    if (late && i >= 2 && tier != Tier.Prismatic && rng.NextDouble() < .18)
                    {
                        rewards.Add(ComponentAward(tier, rng.NextDouble() < .65
                            ? MonsterTacticsItemKind.ItemRemover : MonsterTacticsItemKind.Reforger));
                    }
                    else if (tier == Tier.Prismatic)
                    {
                        Award prize = ComponentAward(tier, NextComponent(rng, componentBag, ref bagCursor));
                        prize.HasBonus = true;
                        prize.BonusComponent = NextComponent(rng, componentBag, ref bagCursor);
                        rewards.Add(prize);
                    }
                    else if (i == 0 || (late && i == 1) || (tier == Tier.Gold && rng.NextDouble() < .6))
                        rewards.Add(ComponentAward(tier, NextComponent(rng, componentBag, ref bagCursor)));
                    else rewards.Add(CoinAward(tier, CoinValue(tier, rng)));
                }
                // U6.9.3: one guaranteed CHOICE capsule on each late PvE boss,
                // on top of the existing fair-match drops (never replaces them).
                if (stage == 4) rewards.Add(CapsuleAward(MonsterTacticsItemCapsuleKind.Parts));
                if (stage == 5 || stage == 6)
                    rewards.Add(CapsuleAward(MonsterTacticsItemCapsuleKind.Gear));
                plan[Key(stage, number)] = rewards;
            }
        }

        private static int CoinValue(Tier tier, System.Random rng)
        {
            switch (tier)
            {
                case Tier.Silver: return rng.Next(1, 3);
                case Tier.Blue: return rng.Next(2, 5);
                case Tier.Gold: return rng.Next(4, 7);
                default: return 8;
            }
        }

        private static Tier ChooseTier(System.Random rng, bool late)
        {
            int roll = rng.Next(1000);
            if (!late) return roll < 450 ? Tier.Silver : roll < 850 ? Tier.Blue : Tier.Gold;
            return roll < 200 ? Tier.Silver : roll < 700 ? Tier.Blue : Tier.Gold;
        }

        private static Award ComponentAward(Tier tier, MonsterTacticsItemKind item) => new Award
        { Tier = tier, IsComponent = true, Component = item, Coins = 0 };

        private static Award CoinAward(Tier tier, int coins) => new Award
        { Tier = tier, IsComponent = false, Coins = coins };

        private static Award CapsuleAward(MonsterTacticsItemCapsuleKind kind) => new Award
        { Tier = kind == MonsterTacticsItemCapsuleKind.Parts ? Tier.Blue : Tier.Gold,
          IsCapsule = true, CapsuleKind = kind };

        private static Award MonsterAward(Tier tier, string familyName) => new Award
        { Tier = tier, IsMonster = true, MonsterFamily = familyName, Coins = 0 };

        /// <summary>Default Monster costs in test loot balls (not shop odds).</summary>
        public static bool MonsterCostMatchesTier(Tier tier, int cost)
        {
            switch (tier)
            {
                case Tier.Silver: return cost == 1 || cost == 2;
                case Tier.Blue: return cost == 2 || cost == 3;
                case Tier.Gold: return cost == 3 || cost == 4;
                case Tier.Prismatic: return cost == 4 || cost == 5;
                default: return false;
            }
        }

        private static MonsterTacticsItemKind NextComponent(System.Random rng, List<MonsterTacticsItemKind> bag, ref int at)
        {
            if (at >= bag.Count)
            {
                bag.Clear();
                bag.AddRange(MonsterTacticsItemTypes.Components);
                Shuffle(bag, rng);
                at = 0;
            }
            return bag[at++];
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }

        public List<Award> PlanFor(int stage, int round)
        {
            PreparePlan();
            List<Award> rewards;
            return plan.TryGetValue(Key(stage, round), out rewards) ? new List<Award>(rewards) : new List<Award>();
        }

        public string PlanSummary()
        {
            PreparePlan();
            var keys = new List<string>(plan.Keys);
            keys.Sort((a, b) =>
            {
                string[] x = a.Split('-'), y = b.Split('-');
                int cmp = int.Parse(x[0]).CompareTo(int.Parse(y[0]));
                return cmp != 0 ? cmp : int.Parse(x[1]).CompareTo(int.Parse(y[1]));
            });
            string text = "Seed: " + seed + "  |  Prismatic match: " + (MasterBallScheduled ? "YES" : "NO") + "\n";
            foreach (string k in keys)
            {
                text += k + ": ";
                foreach (Award a in plan[k]) text += a + ";  ";
                text += "\n";
            }
            return text;
        }

        public void ReleaseForEncounter(int stage, int round, MonsterTacticsU62RoundManager.RoundKind kind)
        {
            if (kind != MonsterTacticsU62RoundManager.RoundKind.PvE && kind != MonsterTacticsU62RoundManager.RoundKind.Boss) return;
            PreparePlan();
            string key = Key(stage, round);
            if (!releasedEncounters.Add(key)) return;
            List<Award> rewards;
            if (!plan.TryGetValue(key, out rewards)) return;

            List<Vector3> origins = FindPveDropOrigins();
            for (int i = 0; i < rewards.Count; i++)
            {
                Vector3 origin = origins.Count > 0 ? origins[i % origins.Count] : FindSpawnPoint(i, stage, round) + Vector3.up * .35f;
                SpawnOrb(rewards[i], i, stage, round, origin);
            }
            // U6.11.9: roll at encounter release, once, from a separate deterministic
            // encounter RNG. Never consume or replace guaranteed gold/item awards.
            Award monsterReward;
            if (TryRollAutoPveMonsterAward(stage, round, kind, out monsterReward))
            {
                int index = rewards.Count;
                Vector3 origin = origins.Count > 0 ? origins[index % origins.Count] :
                    FindSpawnPoint(index, stage, round) + Vector3.up * .35f;
                SpawnOrb(monsterReward, index, stage, round, origin);
                autoPveMonsterBallsSpawned++;
                Debug.Log("[MonsterTactics U6.11.9] " + key +
                    " dropped EXTRA " + monsterReward + ".");
            }
            Debug.Log("[MonsterTactics U6.7.2] " + key + " launched " + rewards.Count + " TFT-style loot drops.");
        }

        /// <summary>
        /// Cost ceilings are stage-based, independent of shop level. Master Balls
        /// are opt-in and can only appear from Stage 7 onward.
        /// </summary>
        private Tier MonsterTierForStage(int stage, System.Random rng)
        {
            if (stage <= 2) return Tier.Silver;
            if (stage == 3) return Tier.Blue;
            if (stage == 4) return rng.NextDouble() < .25 ? Tier.Gold : Tier.Blue;
            if (stage >= 7 && allowLateMasterMonsterBalls && rng.NextDouble() < .10)
                return Tier.Prismatic;
            return Tier.Gold;
        }

        private bool TryChooseMonsterAward(Tier tier, System.Random rng, out Award award)
        {
            award = default(Award);
            if (shop == null) shop = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (shop == null || !shop.IsShopReady) return false;
            var candidates = new List<MonsterTacticsU5Roster.Family>();
            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.ActiveFamilies)
            {
                if (family == null || family.Forms == null || family.Forms.Length != 3) continue;
                if (!MonsterCostMatchesTier(tier, family.Cost)) continue;
                if (!shop.CanOfferLootMonster(family.Forms[0])) continue;
                candidates.Add(family);
            }
            if (candidates.Count == 0) return false;
            // Sort first so seeded results are stable across catalog iteration order.
            candidates.Sort((left, right) => string.Compare(left.Forms[0], right.Forms[0],
                StringComparison.OrdinalIgnoreCase));
            award = MonsterAward(tier, candidates[rng.Next(candidates.Count)].Forms[0]);
            return true;
        }

        private bool TryRollAutoPveMonsterAward(int stage, int round,
            MonsterTacticsU62RoundManager.RoundKind kind, out Award award)
        {
            award = default(Award);
            if (!enableAutoPveMonsterBalls) return false;
            if (stage <= 1 && !includeStageOneMonsterBalls) return false;
            if (kind != MonsterTacticsU62RoundManager.RoundKind.PvE &&
                kind != MonsterTacticsU62RoundManager.RoundKind.Boss) return false;
            float chance = kind == MonsterTacticsU62RoundManager.RoundKind.Boss ?
                bossPveMonsterChance : regularPveMonsterChance;
            if (chance <= 0f) return false;
            // A second, encounter-specific RNG never changes the preplanned
            // component/prismatic allocations or an existing shop RNG sequence.
            System.Random rng = new System.Random(unchecked(seed ^ (stage * 73856093) ^
                (round * 19349663) ^ 0x2b992ddf));
            if (rng.NextDouble() >= chance) return false;
            return TryChooseMonsterAward(MonsterTierForStage(stage, rng), rng, out award);
        }

        /// <summary>
        /// Manual smoke test of the exact stage tier/eligibility logic; bypasses
        /// the optional chance and does NOT consume a real encounter's payout.
        /// </summary>
        public bool DebugSpawnStageMonsterBall(int stage, out string message)
        {
            message = "";
            if (!Application.isPlaying)
            {
                message = "Enter Play Mode first.";
                return false;
            }
            PreparePlan();
            stage = Mathf.Clamp(stage, 1, 8);
            System.Random rng = new System.Random(unchecked(seed ^
                (stage * 83492791) ^ (active.Count * 19349663) ^ 0x4ab83f11));
            Tier tier = MonsterTierForStage(stage, rng);
            Award award;
            if (!TryChooseMonsterAward(tier, rng, out award))
            {
                message = "No elidunebit " + tier + " Monster. Check the shop and roster.";
                return false;
            }
            int index = active.Count;
            Vector3 origin = FindSpawnPoint(index, stage, 1) + Vector3.up * 1.15f;
            SpawnOrb(award, index, stage, 1, origin);
            message = "Stage " + stage + " test: " + award + ". Collect the ball with trainer.";
            return true;
        }

        public void DebugSpawn(Tier tier)
        {
            PreparePlan();
            System.Random r = new System.Random(seed + active.Count * 101 + 41);
            Award reward = tier == Tier.Prismatic || tier == Tier.Gold || tier == Tier.Blue
                ? ComponentAward(tier, MonsterTacticsItemTypes.Components[r.Next(MonsterTacticsItemTypes.Components.Length)])
                : CoinAward(tier, 2);
            if (tier == Tier.Prismatic)
            {
                reward.HasBonus = true;
                reward.BonusComponent = MonsterTacticsItemTypes.Components[r.Next(MonsterTacticsItemTypes.Components.Length)];
            }
            SpawnOrb(reward, active.Count, 1, 1, transform.position + Vector3.up * .4f);
        }

        /// <summary>
        /// U6.11.8 (opt-in TEST): spawn one physical Monster loot ball without
        /// modifying the match reward plan. Empty familyName = random compatible
        /// enabled family. Exact family requests are validated against the tier.
        /// </summary>
        public bool DebugSpawnMonster(Tier tier, string familyName, out string result)
        {
            result = "";
            if (!Application.isPlaying)
            {
                result = "Enter Play Mode first.";
                return false;
            }
            if (shop == null) shop = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (shop == null || !shop.IsShopReady)
            {
                result = "Monster shop not initialized yet.";
                return false;
            }
            var candidates = new List<MonsterTacticsU5Roster.Family>();
            foreach (MonsterTacticsU5Roster.Family f in MonsterTacticsU5Roster.ActiveFamilies)
                if (f != null && f.Forms != null && f.Forms.Length == 3 &&
                    MonsterCostMatchesTier(tier, f.Cost) && shop.CanOfferLootMonster(f.Forms[0]))
                    candidates.Add(f);

            if (!string.IsNullOrWhiteSpace(familyName))
            {
                candidates.RemoveAll(f => !string.Equals(f.Forms[0], familyName.Trim(),
                    StringComparison.OrdinalIgnoreCase));
            }
            if (candidates.Count == 0)
            {
                result = "No elidunebit Monster for " + tier +
                    (string.IsNullOrWhiteSpace(familyName) ? " ball." : " ball: " + familyName +
                    ". Check its cost, enabled state and 3★ completion status.");
                return false;
            }

            MonsterTacticsU5Roster.Family chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            int index = active.Count;
            Vector3 origin = FindSpawnPoint(index, 2, 1) + Vector3.up * 1.20f;
            SpawnOrb(MonsterAward(tier, chosen.Forms[0]), index, 2, 1, origin);
            result = tier + " Monster ball spawned (+1 " + chosen.Forms[0] +
                "). Collect with trainer, or use Collect Waiting Balls.";
            Debug.Log("[MonsterTactics U6.11.8] " + result, this);
            return true;
        }

        /// <summary>
        /// U6.9.6 augment supply drop. Spawn physical anvils on arena hexes; NEVER
        /// award items or claim a Monster bench slot directly. The ordinary loot
        /// pickup logic holds each anvil on the ground if all nine bench slots
        /// are occupied, so the reward cannot be silently lost.
        /// </summary>
        public int SpawnAugmentCapsules(MonsterTacticsItemCapsuleKind kind, int count, string source)
        {
            if (count <= 0) return 0;
            // Protect against misconfigured augment quantities while preserving
            // the 2+1 default Champion supply grant.
            count = Mathf.Clamp(count, 1, 10);
            int firstIndex = active.Count;
            for (int i = 0; i < count; i++)
            {
                int index = firstIndex + i;
                // Spawn above the friendly field; the same SpawnOrb code used
                // for PvE loot animates the capsules onto normal hex surfaces.
                Vector3 origin = FindSpawnPoint(index, 2, 1) +
                    new Vector3(0f, 1.10f, 0.22f);
                SpawnOrb(CapsuleAward(kind), index, 2, 1, origin);
            }
            Debug.Log("[MonsterTactics U6.9.6] " + source + " dropped " + count +
                " " + MonsterTacticsItemCapsules.DisplayName(kind) + " on the arena.");
            return count;
        }

        // Test a physical reward capsule without advancing the round.
        public void DebugSpawnCapsule(MonsterTacticsItemCapsuleKind kind)
        {
            PreparePlan();
            SpawnOrb(CapsuleAward(kind), active.Count, 4, 7,
                transform.position + Vector3.up * .4f);
        }

        public void DebugCollectAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) Collect(active[i]);
        }

        private List<Vector3> FindPveDropOrigins()
        {
            var points = new List<Vector3>();
            MonsterUnit[] units = FindObjectsByType<MonsterUnit>();
            for (int i = 0; i < units.Length; i++)
            {
                MonsterUnit unit = units[i];
                if (unit == null || !unit.gameObject.activeInHierarchy) continue;
                string name = unit.gameObject.name ?? string.Empty;
                Transform parent = unit.transform.parent;
                string parentName = parent != null ? parent.name ?? string.Empty : string.Empty;
                if (name.StartsWith("Wild ", StringComparison.Ordinal) ||
                    name.StartsWith("Boss ", StringComparison.Ordinal) ||
                    parentName.Contains("Wild Monster") ||
                    parentName.Contains("Solo Boss"))
                {
                    points.Add(unit.NameplateWorldPosition + Vector3.down * .55f);
                }
            }

            if (points.Count == 0)
            {
                HexTile[] tiles = FindObjectsByType<HexTile>();
                for (int i = 0; i < tiles.Length; i++)
                {
                    HexTile tile = tiles[i];
                    if (tile == null || tile.Row < 5 || tile.Row > 7) continue;
                    points.Add(tile.transform.position + Vector3.up * .45f);
                    if (points.Count >= 3) break;
                }
            }
            return points;
        }

        private void SpawnOrb(Award award, int index, int stage, int round, Vector3 origin)
        {
            Vector3 landing = FindSpawnPoint(index, stage, round);
            Vector3 fan = Vector3.zero;
            if (index > 0)
            {
                float dir = (index % 2 == 0) ? 1f : -1f;
                fan = new Vector3(.16f * dir * (1 + index / 2), 0f, .03f * (index % 3));
            }
            landing += fan;

            float rarityScale = BallScale(award.Tier);
            // FindSpawnPoint returns the hex TOP, then we add an art-aware height.
            landing.y += rarityScale * .46f + .13f;

            var go = new GameObject("Loot " + award.ToString());
            go.transform.SetParent(transform, false);
            go.transform.position = origin;

            var auraObj = new GameObject("Rarity Glow");
            auraObj.transform.SetParent(go.transform, false);
            SpriteRenderer glow = auraObj.AddComponent<SpriteRenderer>();
            glow.sprite = GlowSprite(award.Tier);
            glow.sortingOrder = 300;
            glow.color = RarityColor(award.Tier);
            auraObj.transform.localScale = Vector3.one * rarityScale * 2.05f;

            var visual = new GameObject("Ball Sprite");
            visual.transform.SetParent(go.transform, false);
            SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = award.IsCapsule ? CapsuleSprite(award.CapsuleKind) : BallSprite(award.Tier);
            sr.sortingOrder = 312;
            sr.transform.localScale = Vector3.one * rarityScale;

            active.Add(new Pickup
            {
                Reward = award,
                Root = go.transform,
                Ball = visual.transform,
                Aura = glow,
                Start = origin,
                End = landing,
                Phase = UnityEngine.Random.Range(0f, 6f),
                SpawnTime = Time.time,
                LaunchDuration = launchDuration + index * .04f,
                BounceHeight = launchBounceHeight + .06f * index,
                Settled = false,
                BaseScale = rarityScale
            });
        }

        private Vector3 FindSpawnPoint(int index, int stage, int round)
        {
            HexTile[] tiles = FindObjectsByType<HexTile>();
            int col = (2 + index + round) % 7;
            int row = 2 + (index % 2);
            HexTile nearest = null;
            int score = int.MaxValue;
            foreach (HexTile tile in tiles)
            {
                if (tile == null || tile.Row > 3 || tile.Row < 0) continue;
                int s = Mathf.Abs(tile.Column - col) * 3 + Mathf.Abs(tile.Row - row) * 2;
                if (s < score) { score = s; nearest = tile; }
            }
            Vector3 pt = nearest != null ? nearest.transform.position : transform.position + Vector3.right * (index * .5f);
            return new Vector3(pt.x, TileSurfaceY(nearest, pt.y), pt.z);
        }

        // Hex transform origins sit inside the prism; the collision/render bounds
        // tell us where the visible top surface actually is.
        private static float TileSurfaceY(HexTile tile, float fallback)
        {
            if (tile == null) return fallback;
            Collider collider = tile.GetComponent<Collider>();
            if (collider != null && collider.enabled) return collider.bounds.max.y;
            Renderer renderer = tile.GetComponent<Renderer>();
            return renderer != null ? renderer.bounds.max.y : fallback + .18f;
        }

        private Vector3 CoinGroundPosition(Vector3 near)
        {
            HexTile nearest = null;
            float best = float.MaxValue;
            foreach (HexTile tile in FindObjectsByType<HexTile>())
            {
                if (tile == null) continue;
                Vector3 pos = tile.transform.position;
                float distance = (new Vector2(pos.x, pos.z) - new Vector2(near.x, near.z)).sqrMagnitude;
                if (distance < best) { nearest = tile; best = distance; }
            }
            return new Vector3(near.x, TileSurfaceY(nearest, near.y) + coinWorldSize * .43f + .055f, near.z);
        }

        private float BallScale(Tier tier)
        {
            float mul = 1f;
            switch (tier)
            {
                case Tier.Silver: mul = .94f; break;
                case Tier.Blue: mul = 1.07f; break;
                case Tier.Gold: mul = 1.21f; break;
                case Tier.Prismatic: mul = 1.38f; break;
            }
            return ballWorldSize * mul;
        }

        private Sprite BallSprite(Tier tier)
        {
            Sprite s;
            if (balls.TryGetValue(tier, out s)) return s;
            string name = tier == Tier.Silver ? "MonsterBall" : tier == Tier.Blue ? "GreatBall" : tier == Tier.Gold ? "UltraBall" : "MasterBall";
            Texture2D tex = Resources.Load<Texture2D>("LootBalls/" + name);
            if (tex == null) { Debug.LogWarning("[MonsterTactics Loot] Missing LootBalls/" + name); return null; }
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), tex.width);
            balls[tier] = s;
            return s;
        }

        private Sprite CapsuleSprite(MonsterTacticsItemCapsuleKind kind)
        {
            Sprite sprite;
            if (capsuleSprites.TryGetValue(kind, out sprite)) return sprite;
            string name = kind == MonsterTacticsItemCapsuleKind.Parts ? "MonsterPartCapsule" : "MonsterGearCapsule";
            Texture2D texture = Resources.Load<Texture2D>("ItemIcons/" + name);
            if (texture == null) return BallSprite(kind == MonsterTacticsItemCapsuleKind.Parts ? Tier.Blue : Tier.Gold);
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), texture.width);
            capsuleSprites[kind] = sprite;
            return sprite;
        }

        private Sprite CoinSprite()
        {
            if (coinSprite != null) return coinSprite;
            Texture2D tex = Resources.Load<Texture2D>("LootBalls/GoldCoin");
            if (tex == null) return null;
            coinSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), tex.width);
            return coinSprite;
        }

        private Sprite GlowSprite(Tier tier)
        {
            Sprite s;
            if (glows.TryGetValue(tier, out s)) return s;
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 63.5f;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - dist), 1.5f) * .66f;
                    pixels[y * 128 + x] = new Color(1, 1, 1, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f), 128);
            glows[tier] = s;
            return s;
        }

        private static Color RarityColor(Tier t)
        {
            switch (t)
            {
                case Tier.Silver: return new Color(.74f, .86f, 1f, .88f);
                case Tier.Blue: return new Color(.24f, .56f, 1f, .92f);
                case Tier.Gold: return new Color(1f, .73f, .15f, .96f);
                default: return new Color(.85f, .35f, 1f, .98f);
            }
        }

        private void Update()
        {
            if (cam == null) cam = Camera.main;
            if (trainer == null) trainer = FindAnyObjectByType<TrainerMovement>();
            if (shop == null) shop = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (inventory == null && shop != null) inventory = shop.GetComponent<MonsterTacticsItemInventory>();

            for (int i = active.Count - 1; i >= 0; i--)
            {
                Pickup p = active[i];
                if (p.Root == null) { active.RemoveAt(i); continue; }

                float since = Time.time - p.SpawnTime;
                if (since < p.LaunchDuration)
                {
                    float t = Mathf.Clamp01(since / Mathf.Max(.01f, p.LaunchDuration));
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    Vector3 pos = Vector3.Lerp(p.Start, p.End, eased);
                    pos.y += Mathf.Sin(t * Mathf.PI) * p.BounceHeight;
                    p.Root.position = pos;
                    p.Settled = false;
                }
                else
                {
                    float t = Time.time * 2.2f + p.Phase;
                    p.Root.position = p.End + Vector3.up * (Mathf.Sin(t) * .085f);
                    p.Settled = true;
                }

                if (cam != null)
                {
                    p.Ball.rotation = cam.transform.rotation;
                    p.Aura.transform.rotation = cam.transform.rotation;
                }

                float pulse = 1f + .12f * Mathf.Sin((Time.time * 2.2f + p.Phase) * 1.6f);
                p.Aura.transform.localScale = Vector3.one * p.BaseScale * 2.05f * pulse;

                if (p.Reward.Tier == Tier.Prismatic)
                    p.Aura.color = Color.HSVToRGB(Mathf.Repeat(Time.time * .22f + p.Phase * .13f, 1f), .68f, 1f);

                if (trainer == null || !p.Settled) continue;
                Vector3 d = trainer.transform.position - p.Root.position;
                d.y = 0;
                if (d.sqrMagnitude <= pickupRadius * pickupRadius)
                {
                    if (CanGive(p.Reward)) Collect(p);
                    else if ((p.Reward.IsCapsule || p.Reward.IsMonster) && shop != null &&
                        shop.EmptyBenchSlots <= 0 && Time.unscaledTime >= nextFullNoticeTime)
                    {
                        nextFullNoticeTime = Time.unscaledTime + 2.5f;
                        lastPickup = "Clear your Monster Bench first!";
                        pickupNoticeUntil = Time.unscaledTime + 2.4f;
                        shop.ShowAnvilNotice(lastPickup);
                    }
                }
            }
            UpdateLooseCoins();
        }

        private void UpdateLooseCoins()
        {
            for (int i = looseCoins.Count - 1; i >= 0; --i)
            {
                CoinPickup coin = looseCoins[i];
                if (coin.Root == null) { looseCoins.RemoveAt(i); continue; }
                float t = Mathf.Clamp01((Time.time - coin.SpawnTime) / coin.TravelDuration);
                if (t < 1f)
                {
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    Vector3 pos = Vector3.Lerp(coin.Start, coin.End, eased);
                    pos.y += Mathf.Sin(t * Mathf.PI) * .48f;
                    coin.Root.position = pos;
                    coin.Settled = false;
                }
                else
                {
                    coin.Root.position = coin.End + Vector3.up * (.027f * Mathf.Sin(Time.time * 3f + coin.Phase));
                    coin.Settled = true;
                }
                if (cam != null) coin.Root.rotation = cam.transform.rotation;
                if (coin.Visual != null)
                    coin.Visual.transform.localScale = Vector3.one * coinWorldSize * (1f + .035f * Mathf.Sin(Time.time * 4.5f + coin.Phase));

                if (!coin.Settled || trainer == null || shop == null || !shop.IsShopReady) continue;
                Vector3 delta = trainer.transform.position - coin.Root.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= coinPickupRadius * coinPickupRadius)
                    CollectCoin(coin);
            }
        }

        private bool CanGive(Award a)
        {
            if (shop == null || !shop.IsShopReady) return false;
            if (a.IsMonster) return shop.CanReceiveLootMonster(a.MonsterFamily);
            if (a.IsCapsule) return shop.EmptyBenchSlots > 0 &&
                shop.GetComponent<MonsterTacticsItemCapsules>() != null;
            return !a.IsComponent || inventory != null;
        }

        private void Collect(Pickup p)
        {
            if (p == null || !CanGive(p.Reward)) return;
            // Persist the rolled choice offer before removing the world pickup;
            // if the shop isn't ready, the reward stays on the board instead.
            if (p.Reward.IsMonster)
            {
                // Atomic: only remove the ball after the unit actually reaches
                // the owned bench/evolution system. Full bench keeps it on arena.
                if (!shop.TryGrantLootMonster(p.Reward.MonsterFamily)) return;
            }
            else if (p.Reward.IsCapsule)
            {
                var capsules = shop.GetComponent<MonsterTacticsItemCapsules>();
                if (capsules == null || !capsules.GrantCapsule(p.Reward.CapsuleKind, "pve-loot"))
                    return;
            }
            active.Remove(p);
            Vector3 point = p.End;
            float scale = p.BaseScale;
            Tier tier = p.Reward.Tier;
            Award reward = p.Reward;
            Destroy(p.Root.gameObject);

            // A small clean pop. No giant explosion / fake broken halves.
            StartCoroutine(AnimateOrbOpen(point, tier, scale));

            if (reward.IsMonster)
            {
                lastPickup = "+1 " + reward.MonsterFamily + " from " + reward.Tier + " Creature Orb!";
                pickupNoticeUntil = Time.unscaledTime + 2f;
            }
            else if (reward.IsCapsule)
            {
                lastPickup = "Collected " + MonsterTacticsItemCapsules.DisplayName(reward.CapsuleKind) + " — click on Monster Bench";
                pickupNoticeUntil = Time.unscaledTime + 2f;
            }
            else if (reward.IsComponent)
            {
                inventory.Grant(reward.Component);
                lastPickup = "+ " + MonsterTacticsItemTypes.Name(reward.Component);
                if (reward.HasBonus)
                {
                    inventory.Grant(reward.BonusComponent);
                    lastPickup += " + " + MonsterTacticsItemTypes.Name(reward.BonusComponent);
                }
                pickupNoticeUntil = Time.unscaledTime + 2f;
            }
            else
            {
                // Important: no gold is given here. The orb opens first, then
                // each ground coin gets collected individually (1 coin = 1 gold).
                StartCoroutine(ScatterGoldCoins(point, reward.Coins));
            }
            Debug.Log("[MonsterTactics Loot] Opened " + reward + " at " + point);
        }

        private IEnumerator AnimateOrbOpen(Vector3 position, Tier tier, float baseScale)
        {
            var go = new GameObject("Loot opened");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = BallSprite(tier);
            renderer.sortingOrder = 342;
            go.transform.localScale = Vector3.one * baseScale;
            float elapsed = 0f;
            const float duration = .16f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (cam != null) go.transform.rotation = cam.transform.rotation;
                go.transform.localScale = Vector3.one * baseScale * (1f + .28f * t);
                renderer.color = new Color(1f, 1f, 1f, 1f - t);
                yield return null;
            }
            Destroy(go);
        }

        private IEnumerator ScatterGoldCoins(Vector3 center, int count)
        {
            // Let the ball open before contents emerge.
            yield return new WaitForSeconds(.11f);
            if (CoinSprite() == null)
            {
                // A missing texture must never make earned gold disappear.
                if (shop != null) shop.GrantLootGold(Mathf.Max(0, count));
                yield break;
            }
            for (int i = 0; i < count; i++)
            {
                float theta = (i + .35f) * 2.399963f; // golden-angle distribution
                float radius = count == 1 ? .44f : .50f + .16f * (i % 3);
                Vector3 offset = new Vector3(Mathf.Cos(theta) * radius, 0f, Mathf.Sin(theta) * radius);
                var obj = new GameObject("Gold Coin Pickup (+1)");
                obj.transform.SetParent(transform, false);
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = CoinSprite();
                sr.sortingOrder = 352;
                obj.transform.localScale = Vector3.one * coinWorldSize;
                Vector3 start = center + Vector3.up * .19f;
                Vector3 target = CoinGroundPosition(center + offset);
                obj.transform.position = start;
                looseCoins.Add(new CoinPickup
                {
                    Root = obj.transform,
                    Visual = sr,
                    Start = start,
                    End = target,
                    SpawnTime = Time.time,
                    TravelDuration = .33f + .035f * (i % 3),
                    Phase = i * .91f,
                    Settled = false
                });
            }
            Debug.Log("[MonsterTactics Loot] Gold ball released " + count + " collectible coins (+1 each).");
        }

        private void CollectCoin(CoinPickup coin)
        {
            if (coin == null || !looseCoins.Remove(coin) || shop == null) return;
            shop.GrantLootGold(1);
            lastPickup = "+1 Gold";
            pickupNoticeUntil = Time.unscaledTime + 1.2f;
            Destroy(coin.Root.gameObject);
        }

        public void DebugCollectAllCoins()
        {
            for (int i = looseCoins.Count - 1; i >= 0; --i)
                CollectCoin(looseCoins[i]);
        }

        private void OnGUI()
        {
            if (Time.unscaledTime >= pickupNoticeUntil || string.IsNullOrEmpty(lastPickup)) return;
            if (noticeStyle == null)
            {
                noticeStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                noticeStyle.normal.textColor = new Color(1f, .90f, .57f);
            }
            GUI.depth = -235;
            GUI.Box(new Rect(Screen.width * .5f - 115, 69, 230, 34), lastPickup, noticeStyle);
        }

        private void OnDestroy()
        {
            foreach (Sprite s in balls.Values) if (s != null) Destroy(s);
            foreach (Sprite s in glows.Values) if (s != null) { if (s.texture != null) Destroy(s.texture); Destroy(s); }
            if (coinSprite != null) Destroy(coinSprite);
        }
    }
}
