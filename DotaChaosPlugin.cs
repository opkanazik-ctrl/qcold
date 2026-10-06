using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace DotaChaos
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class DotaChaosPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "peak.dota.chaos";
        public const string PluginName = "Dota Chaos";
        public const string PluginVersion = "0.1.0";

        internal static DotaChaosPlugin Instance;
        internal ConfigEntry<bool> Enabled;
        internal ConfigEntry<float> EventInterval;
        internal ConfigEntry<int> MaxCreeps;
        internal ConfigEntry<float> AbilityDuration;
        internal ConfigEntry<bool> SpawnCreeps;
        internal ConfigEntry<bool> DebugLogging;

        private ChaosRuntime runtime;

        private void Awake()
        {
            Instance = this;

            Enabled = Config.Bind("General", "Enabled", true,
                "Enable the Dota Chaos runtime.");
            EventInterval = Config.Bind("Chaos", "EventIntervalSeconds", 18f,
                "Average time between random Dota-style events.");
            MaxCreeps = Config.Bind("Chaos", "MaxLocalCreeps", 8,
                "Maximum number of prototype chaos creeps created by this client.");
            AbilityDuration = Config.Bind("Chaos", "AbilityDurationSeconds", 8f,
                "Duration of temporary movement effects.");
            SpawnCreeps = Config.Bind("Chaos", "SpawnPrototypeCreeps", true,
                "Spawn simple local prototype creeps. These are not Dota assets.");
            DebugLogging = Config.Bind("Debug", "Verbose", false,
                "Enable verbose diagnostic logging.");

            runtime = gameObject.AddComponent<ChaosRuntime>();
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }

        internal void Log(string message)
        {
            if (DebugLogging.Value)
                Logger.LogInfo("[DotaChaos] " + message);
        }
    }

    internal enum ChaosAbility
    {
        Blink,
        Haste,
        DoubleJump,
        Toss,
        Vacuum,
        Rupture,
        LowGravity,
        ArcaneSurge,
        TinyThrow,
        Chronosphere
    }

    internal sealed class ChaosRuntime : MonoBehaviour
    {
        private readonly List<GameObject> creeps = new List<GameObject>();
        private readonly Dictionary<int, float> buffUntil = new Dictionary<int, float>();

        private Type characterType;
        private FieldInfo activeCharactersField;
        private PropertyInfo activeCharactersProperty;
        private FieldInfo localCharacterField;
        private PropertyInfo localCharacterProperty;

        private float nextEvent;
        private float nextCreepWave;
        private System.Random rng;

        private void Start()
        {
            rng = new System.Random(Environment.TickCount);
            ResolvePeakTypes();
            ScheduleNextEvent();
            nextCreepWave = Time.time + 25f;
        }

        private void Update()
        {
            if (!DotaChaosPlugin.Instance.Enabled.Value)
                return;

            if (Time.time >= nextEvent)
            {
                TriggerRandomEvent();
                ScheduleNextEvent();
            }

            if (DotaChaosPlugin.Instance.SpawnCreeps.Value && Time.time >= nextCreepWave)
            {
                SpawnCreepWave();
                nextCreepWave = Time.time + 55f;
            }

            UpdateCreeps();
            CleanupBuffs();
        }

        private void ResolvePeakTypes()
        {
            characterType = FindType("Character");
            if (characterType == null)
            {
                DotaChaosPlugin.Instance.Logger.LogWarning(
                    "[DotaChaos] Could not find PEAK Character type. Runtime will stay idle.");
                return;
            }

            activeCharactersField = characterType.GetField(
                "ACTIVE_CHARACTERS",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            activeCharactersProperty = characterType.GetProperty(
                "ACTIVE_CHARACTERS",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            localCharacterField = characterType.GetField(
                "localCharacter",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            localCharacterProperty = characterType.GetProperty(
                "localCharacter",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            DotaChaosPlugin.Instance.Log("Resolved PEAK Character reflection API.");
        }

        private void ScheduleNextEvent()
        {
            float baseInterval = Mathf.Max(5f, DotaChaosPlugin.Instance.EventInterval.Value);
            nextEvent = Time.time + Mathf.Lerp(baseInterval * 0.55f, baseInterval * 1.45f,
                (float)rng.NextDouble());
        }

        private void TriggerRandomEvent()
        {
            List<Component> players = GetCharacters().Where(c => c != null).ToList();
            if (players.Count == 0)
                return;

            ChaosAbility ability = (ChaosAbility)rng.Next(Enum.GetValues(typeof(ChaosAbility)).Length);
            Component target = players[rng.Next(players.Count)];

            switch (ability)
            {
                case ChaosAbility.Blink:
                    Blink(target);
                    break;
                case ChaosAbility.Haste:
                    Haste(target);
                    break;
                case ChaosAbility.DoubleJump:
                    GrantExtraJumps(target);
                    break;
                case ChaosAbility.Toss:
                    Toss(target);
                    break;
                case ChaosAbility.Vacuum:
                    Vacuum(target, players);
                    break;
                case ChaosAbility.Rupture:
                    Rupture(target);
                    break;
                case ChaosAbility.LowGravity:
                    LowGravity(target);
                    break;
                case ChaosAbility.ArcaneSurge:
                    ArcaneSurge(target);
                    break;
                case ChaosAbility.TinyThrow:
                    TinyThrow(target);
                    break;
                case ChaosAbility.Chronosphere:
                    Chronosphere(target, players);
                    break;
            }

            DotaChaosPlugin.Instance.Logger.LogInfo(
                $"[DotaChaos] {ability} -> {GetDisplayName(target)}");
        }

        private void Blink(Component target)
        {
            Transform t = target.transform;
            Vector3 forward = t.forward;
            Vector3 destination = t.position + forward * 8f + Vector3.up * 1.2f;
            t.position = destination;
        }

        private void Haste(Component target)
        {
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(target.transform.forward * 12f, ForceMode.VelocityChange);

            MarkBuff(target, DotaChaosPlugin.Instance.AbilityDuration.Value);
        }

        private void GrantExtraJumps(Component target)
        {
            // Prefer the real CharacterMovement.extraJumps field/property when present.
            object movement = GetMemberValue(target, "CharacterMovement");
            if (movement == null)
                movement = FindComponentByTypeName(target.gameObject, "CharacterMovement");

            if (movement != null)
            {
                TryIncrementNumericMember(movement, "extraJumps", 2);
                TryIncrementNumericMember(movement, "jumpsRemaining", 2);
            }

            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(Vector3.up * 4f, ForceMode.VelocityChange);
        }

        private void Toss(Component target)
        {
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb == null)
                return;

            Vector3 dir = Vector3.up * 1.4f + target.transform.forward * 0.7f;
            rb.AddForce(dir.normalized * 13f, ForceMode.VelocityChange);
        }

        private void Vacuum(Component target, List<Component> players)
        {
            Vector3 center = target.transform.position;
            foreach (Component player in players)
            {
                if (player == null) continue;
                if (Vector3.Distance(center, player.transform.position) > 15f) continue;

                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb == null) continue;

                Vector3 pull = center - player.transform.position;
                if (pull.sqrMagnitude > 0.1f)
                    rb.AddForce(pull.normalized * 7f, ForceMode.VelocityChange);
            }
        }

        private void Rupture(Component target)
        {
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb == null)
                return;

            // Prototype interpretation of Rupture: repeated directional impulses.
            StartCoroutine(RuptureRoutine(rb, 4));
        }

        private IEnumerator RuptureRoutine(Rigidbody rb, int pulses)
        {
            for (int i = 0; i < pulses; i++)
            {
                if (rb == null) yield break;
                rb.AddForce(UnityEngine.Random.onUnitSphere * 3.5f, ForceMode.VelocityChange);
                yield return new WaitForSeconds(0.45f);
            }
        }

        private void LowGravity(Component target)
        {
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb == null) return;

            StartCoroutine(GravityRoutine(rb, 5.5f));
        }

        private IEnumerator GravityRoutine(Rigidbody rb, float seconds)
        {
            if (rb == null) yield break;
            bool oldUseGravity = rb.useGravity;
            rb.useGravity = false;
            yield return new WaitForSeconds(seconds);
            if (rb != null) rb.useGravity = oldUseGravity;
        }

        private void ArcaneSurge(Component target)
        {
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(Vector3.up * 7f + target.transform.forward * 3f,
                    ForceMode.VelocityChange);

            MarkBuff(target, DotaChaosPlugin.Instance.AbilityDuration.Value);
        }

        private void TinyThrow(Component target)
        {
            foreach (Component other in GetCharacters())
            {
                if (other == null || other == target) continue;
                Vector3 delta = other.transform.position - target.transform.position;
                if (delta.magnitude > 10f) continue;

                Rigidbody rb = other.GetComponent<Rigidbody>();
                if (rb != null)
                    rb.AddForce((delta.normalized + Vector3.up * 0.5f) * 8f,
                        ForceMode.VelocityChange);
            }
        }

        private void Chronosphere(Component target, List<Component> players)
        {
            Vector3 center = target.transform.position;
            foreach (Component player in players)
            {
                if (player == null) continue;
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb == null) continue;

                if (Vector3.Distance(center, player.transform.position) <= 6f)
                    StartCoroutine(FreezeRoutine(rb, 2.2f));
            }
        }

        private IEnumerator FreezeRoutine(Rigidbody rb, float seconds)
        {
            if (rb == null) yield break;
            bool wasKinematic = rb.isKinematic;
            Vector3 oldVelocity = rb.velocity;
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
            yield return new WaitForSeconds(seconds);
            if (rb != null)
            {
                rb.isKinematic = wasKinematic;
                if (!wasKinematic) rb.velocity = oldVelocity;
            }
        }

        private void SpawnCreepWave()
        {
            int max = Mathf.Clamp(DotaChaosPlugin.Instance.MaxCreeps.Value, 0, 32);
            if (creeps.Count >= max || max == 0)
                return;

            Component target = GetLocalCharacter();
            if (target == null)
                return;

            int count = Mathf.Min(2 + rng.Next(3), max - creeps.Count);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = UnityEngine.Random.onUnitSphere * 6f;
                offset.y = Mathf.Abs(offset.y) + 2f;
                GameObject creep = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                creep.name = "DotaChaos_Creep";
                creep.transform.position = target.transform.position + offset;
                creep.transform.localScale = new Vector3(0.65f, 0.9f, 0.65f);

                Rigidbody rb = creep.AddComponent<Rigidbody>();
                rb.mass = 1.5f;
                rb.drag = 1.2f;

                CreepBrain brain = creep.AddComponent<CreepBrain>();
                brain.SetTarget(target.transform);
                creeps.Add(creep);
            }

            DotaChaosPlugin.Instance.Log($"Spawned {count} prototype creeps.");
        }

        private void UpdateCreeps()
        {
            for (int i = creeps.Count - 1; i >= 0; i--)
            {
                GameObject go = creeps[i];
                if (go == null)
                {
                    creeps.RemoveAt(i);
                    continue;
                }

                CreepBrain brain = go.GetComponent<CreepBrain>();
                if (brain != null && brain.Target == null)
                    brain.SetTarget(GetLocalCharacter()?.transform);

                int max = Mathf.Clamp(DotaChaosPlugin.Instance.MaxCreeps.Value, 0, 32);
                if (i >= max)
                {
                    Destroy(go);
                    creeps.RemoveAt(i);
                }
            }
        }

        private IEnumerable<Component> GetCharacters()
        {
            if (characterType == null)
                yield break;

            object raw = null;
            try
            {
                if (activeCharactersField != null)
                    raw = activeCharactersField.GetValue(null);
                else if (activeCharactersProperty != null)
                    raw = activeCharactersProperty.GetValue(null, null);
            }
            catch (Exception e)
            {
                DotaChaosPlugin.Instance.Log("ACTIVE_CHARACTERS read failed: " + e.Message);
            }

            if (raw is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                    if (item is Component c)
                        yield return c;
            }
        }

        private Component GetLocalCharacter()
        {
            if (characterType == null)
                return null;

            try
            {
                object raw = null;
                if (localCharacterField != null)
                    raw = localCharacterField.GetValue(null);
                else if (localCharacterProperty != null)
                    raw = localCharacterProperty.GetValue(null, null);

                return raw as Component;
            }
            catch
            {
                return null;
            }
        }

        private static Type FindType(string name)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try
                {
                    t = asm.GetType(name, false) ??
                        asm.GetTypes().FirstOrDefault(x => x.Name == name);
                }
                catch { }
                if (t != null) return t;
            }
            return null;
        }

        private static object GetMemberValue(object obj, string name)
        {
            if (obj == null) return null;
            Type t = obj.GetType();

            FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) return f.GetValue(obj);

            PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return p != null && p.CanRead ? p.GetValue(obj, null) : null;
        }

        private static Component FindComponentByTypeName(GameObject go, string typeName)
        {
            foreach (Component c in go.GetComponents<Component>())
                if (c != null && c.GetType().Name == typeName)
                    return c;
            return null;
        }

        private static bool TryIncrementNumericMember(object obj, string name, int amount)
        {
            Type t = obj.GetType();
            FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && IsNumeric(f.FieldType))
            {
                object old = f.GetValue(obj);
                f.SetValue(obj, Convert.ChangeType(Convert.ToDouble(old) + amount, f.FieldType));
                return true;
            }

            PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanRead && p.CanWrite && IsNumeric(p.PropertyType))
            {
                object old = p.GetValue(obj, null);
                p.SetValue(obj, Convert.ChangeType(Convert.ToDouble(old) + amount, p.PropertyType), null);
                return true;
            }

            return false;
        }

        private static bool IsNumeric(Type type)
        {
            return type == typeof(int) || type == typeof(float) || type == typeof(double) ||
                   type == typeof(long) || type == typeof(short);
        }

        private void MarkBuff(Component target, float duration)
        {
            int id = target.GetInstanceID();
            buffUntil[id] = Time.time + duration;
        }

        private void CleanupBuffs()
        {
            if (buffUntil.Count == 0) return;
            List<int> expired = buffUntil
                .Where(x => x.Value <= Time.time)
                .Select(x => x.Key)
                .ToList();

            foreach (int id in expired)
                buffUntil.Remove(id);
        }

        private static string GetDisplayName(Component c)
        {
            if (c == null) return "unknown";
            try
            {
                PropertyInfo p = c.GetType().GetProperty(
                    "characterName",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null)
                {
                    object v = p.GetValue(c, null);
                    if (v != null) return v.ToString();
                }
            }
            catch { }
            return c.gameObject.name;
        }

        private sealed class CreepBrain : MonoBehaviour
        {
            internal Transform Target { get; private set; }

            internal void SetTarget(Transform target)
            {
                Target = target;
            }

            private void FixedUpdate()
            {
                if (Target == null) return;

                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb == null) return;

                Vector3 delta = Target.position - transform.position;
                if (delta.sqrMagnitude < 1.5f * 1.5f) return;

                Vector3 dir = delta.normalized;
                rb.AddForce(dir * 2.2f, ForceMode.Acceleration);

                if (rb.velocity.magnitude > 5f)
                    rb.velocity = rb.velocity.normalized * 5f;
            }
        }
    }
}
