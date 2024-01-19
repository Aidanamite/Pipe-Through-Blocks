using HarmonyLib;
using HMLLibrary;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;
using I2.Loc;
using UnityEngine.SceneManagement;
using UnityEngine.Experimental.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;


namespace PipeThroughBlocks
{
    public class Main : Mod
    {
        Harmony harmony;
        public NewItemData[] creationData = new NewItemData[]
        {
            new NewItemData() { BaseUniqueIndex = 384, UniqueIndex = 1000384, UniqueName = "Block_Floor_Pipe", Localization = "Piped Wooden Floor@another merp", Position = new[]{ Vector3.down * 0.1f }, Scale = new[] { new Vector3(0.5f,0.8f,0.5f) } },
            new NewItemData() { BaseUniqueIndex = 410, UniqueIndex = 1000410, UniqueName = "Block_Wall_Pipe_Half", Localization = "Piped Half Wooden Wall@another merp", Position = new[]{ Vector3.up * 0.15f }, Scale = new[] { Vector3.one * 0.5f } },
            new NewItemData() { BaseUniqueIndex = 409, UniqueIndex = 1000409, UniqueName = "Block_Wall_Pipe", Localization = "Piped Wooden Wall@another merp", Position = new[]{ Vector3.up * 0.15f, Vector3.up * (0.15f + BlockCreator.HalfFloorHeight) }, Scale = new[] { Vector3.one * 0.5f, Vector3.one * 0.5f } },
            new NewItemData() { BaseUniqueIndex = 6973, UniqueIndex = 1006973, UniqueName = "Block_Glass_Floor_Pipe", Localization = "Piped Glass Floor@another merp", Position = new[]{ Vector3.down * 0.1f }, Scale = new[] { new Vector3(0.5f,0.8f,0.5f) } },
            new NewItemData() { BaseUniqueIndex = 6977, UniqueIndex = 1006977, UniqueName = "Block_Glass_Wall_Pipe_Half", Localization = "Piped Half Glass Wall@another merp", Position = new[]{ Vector3.up * 0.15f }, Scale = new[] { Vector3.one * 0.5f } },
            new NewItemData() { BaseUniqueIndex = 6976, UniqueIndex = 1006976, UniqueName = "Block_Glass_Wall_Pipe", Localization = "Piped Glass Wall@another merp", Position = new[]{ Vector3.up * 0.15f, Vector3.up * (0.15f + BlockCreator.HalfFloorHeight) }, Scale = new[] { Vector3.one * 0.5f, Vector3.one * 0.5f } }
        };
        public List<(Item_Base, Item_Base)> newItems = new List<(Item_Base, Item_Base)>();
        public List<Object> createdObjects = new List<Object>();
        public Texture2D overlay;
        public int[] horizontalItems = new[] { 1000548 };
        public Item_Base pipeUpgrade;
        public LanguageSourceData language;
        MeshFilter pipeModel;
        Transform prefabHolder;
        public static Main instance;
        static Button unloadButton;
        static Button.ButtonClickedEvent eventStore = null;
        static bool CanUnload
        {
            get { return eventStore == null; }
            set
            {
                if (!value && eventStore == null)
                {
                    eventStore = unloadButton.onClick;
                    unloadButton.onClick = new Button.ButtonClickedEvent();
                    unloadButton.onClick.AddListener(delegate { Debug.LogError($"[{instance.modlistEntry.jsonmodinfo.name}]: Mod cannot be unloaded while in a multiplayer"); });
                }
                else if (value && eventStore != null)
                {
                    unloadButton.onClick = eventStore;
                    eventStore = null;
                }
            }
        }
        bool loaded = false;
        public void Start()
        {
            unloadButton = modlistEntry.modinfo.unloadBtn.GetComponent<Button>();
            if (SceneManager.GetActiveScene().name == Raft_Network.GameSceneName && ComponentManager<Raft_Network>.Value.remoteUsers.Count > 1)
            {
                Debug.LogError($"[{modlistEntry.jsonmodinfo.name}]: This cannot be loaded while in a multiplayer");
                unloadButton.onClick.Invoke();
                return;
            }
            loaded = true;
            instance = this;
            prefabHolder = new GameObject("prefabHolder").transform;
            prefabHolder.gameObject.SetActive(false);
            createdObjects.Add(prefabHolder.gameObject);
            DontDestroyOnLoad(prefabHolder.gameObject);

            overlay = LoadImage("iconOverlay.png", true);

            language = new LanguageSourceData()
            {
                mDictionary = new Dictionary<string, TermData>
                {
                    ["Item/Block_Upgrade_Pipe"] = new TermData() { Languages = new[] { "Replaced with piped solid wood@" } }
                },
                mLanguages = new List<LanguageData> { new LanguageData() { Code = "en", Name = "English" } }
            };
            LocalizationManager.Sources.Add(language);

            var baseUpgrade = ItemManager.GetItemByIndex(548);
            pipeUpgrade = baseUpgrade.Clone(1000548, "Block_Upgrade_Pipe");
            pipeUpgrade.name = pipeUpgrade.UniqueName;
            pipeUpgrade.settings_Inventory.LocalizationTerm = "Item/" + pipeUpgrade.UniqueName;
            newItems.Add((baseUpgrade, pipeUpgrade));
            createdObjects.Add(pipeUpgrade);
            var t = pipeUpgrade.settings_Inventory.Sprite.texture.GetReadable(pipeUpgrade.settings_Inventory.Sprite.rect);
            t.AddOverlay();
            var t2 = new Texture2D(t.width, t.height, t.format, false);
            t2.SetPixels(t.GetPixels(0));
            Destroy(t);
            t2.Apply(true, true);
            createdObjects.Add(t2);
            pipeUpgrade.settings_Inventory.Sprite = t2.ToSprite();
            createdObjects.Add(pipeUpgrade.settings_Inventory.Sprite);
            ItemManager.GetAllItems().Add(pipeUpgrade);

            pipeModel = ItemManager.GetItemByIndex(277).settings_buildable.GetBlockPrefab(0).GetComponentInChildren<MeshFilter>();
            
            foreach (var item in creationData)
                if (ItemManager.GetItemByIndex(item.BaseUniqueIndex))
                    CreatePipeItem(item);

            Traverse.Create(typeof(LocalizationManager)).Field("OnLocalizeEvent").GetValue<LocalizationManager.OnLocalizeCallback>().Invoke();
            ModUtils_ReloadBuildMenu();
            (harmony = new Harmony("com.aidanamite.PipeThroughBlocks")).PatchAll();
            Log("Mod has been loaded!");
        }

        public void OnModUnload()
        {
            if (!loaded)
                return;
            loaded = false;
            ModUtils_ReloadBuildMenu();
            harmony.UnpatchAll(harmony.Id);
            LocalizationManager.Sources.Remove(language);
            ItemManager.GetAllItems().RemoveAll(x => newItems.Exists(y => y.Item2.UniqueIndex == x.UniqueIndex));
            foreach (var q in Resources.FindObjectsOfTypeAll<SO_BlockQuadType>())
                Traverse.Create(q).Field("acceptableBlockTypes").GetValue<List<Item_Base>>().RemoveAll(x => newItems.Exists(y => y.Item2.UniqueIndex == x.UniqueIndex));
            foreach (var q in Resources.FindObjectsOfTypeAll<SO_BlockCollisionMask>())
                Traverse.Create(q).Field("blockTypesToIgnore").GetValue<List<Item_Base>>().RemoveAll(x => newItems.Exists(y => y.Item2.UniqueIndex == x.UniqueIndex));
            foreach (var block in BlockCreator.GetPlacedBlocks().ToArray())
                if (block.buildableItem != null && newItems.Exists(y => y.Item2.UniqueIndex == block.buildableItem.UniqueIndex))
                    BlockCreator.RemoveBlock(block, null, false);
            foreach (var o in createdObjects)
                Destroy(o);
            Log("Mod has been unloaded!");
        }

        float time = 0;
        void Update()
        {
            time += Time.deltaTime;
            if (time > 1)
            {
                time %= 1;
                var c = 0;
                foreach (var item in creationData)
                    if (!item.BaseItem && ItemManager.GetItemByIndex(item.BaseUniqueIndex))
                    {
                        CreatePipeItem(item);
                        c++;
                    }
                if (c > 0)
                    ModUtils_ReloadBuildMenu();
            }
        }

        List<(Item_Base, Item_Base, bool)> ModUtils_BuildMenuItems()
        {
            Debug.Log("ModUtils build menu has been requested. ?" + (loaded ? newItems.Count : default(int?)));
            if (!loaded) return null;
            var l = new List<(Item_Base, Item_Base, bool)>();
            foreach (var i in newItems)
                l.Add((i.Item1, i.Item2, horizontalItems.Contains(i.Item2.UniqueIndex)));
            return l;
        }

        void ModUtils_ReloadBuildMenu() { Debug.Log("Attempted to reload build menu but ModUtils has not been implemented"); }

        static void CheckUnload() => CanUnload = SceneManager.GetActiveScene().name != Raft_Network.GameSceneName || ComponentManager<Raft_Network>.Value.remoteUsers.Count <= 1;

        public override void WorldEvent_OnPlayerConnected(CSteamID steamid, RGD_Settings_Character characterSettings) => CheckUnload();
        public override void WorldEvent_OnPlayerDisconnected(CSteamID steamid, DisconnectReason disconnectReason) => CheckUnload();
        public override void WorldEvent_WorldLoaded() => CheckUnload();
        public override void WorldEvent_WorldUnloaded() => CheckUnload();

        public Texture2D LoadImage(string filename, bool leaveReadable = false, GraphicsFormat format = GraphicsFormat.B8G8R8A8_SRGB)
        {
            var t = new Texture2D(0, 0, format, TextureCreationFlags.None);
            t.LoadImage(GetEmbeddedFileBytes(filename), !leaveReadable);
            if (leaveReadable)
                t.Apply();
            createdObjects.Add(t);
            return t;
        }

        public static void CreatePipeItem(NewItemData item)
        {
            var baseItem = ItemManager.GetItemByIndex(item.BaseUniqueIndex);
            var newItem = baseItem.Clone(item.UniqueIndex, item.UniqueName);
            instance.createdObjects.Add(newItem);
            newItem.name = newItem.UniqueName;
            newItem.settings_Inventory.LocalizationTerm = "Item/" + newItem.UniqueName;
            try
            {
                instance.language.mDictionary.Add(newItem.settings_Inventory.LocalizationTerm, new TermData() { Languages = new[] { item.Localization } });
                Traverse.Create(newItem.settings_buildable).Field("upgrades").SetValue(new ItemInstance_Buildable.Upgrade());
                newItem.settings_buildable.Upgrades.CopyFieldsOf(baseItem.settings_buildable.Upgrades);
                newItem.settings_buildable.Upgrades.ReplaceValues(null, baseItem);
                var t = newItem.settings_Inventory.Sprite.texture.GetReadable(newItem.settings_Inventory.Sprite.rect);
                t.AddOverlay();
                var t2 = new Texture2D(t.width, t.height, t.format, false);
                t2.SetPixels(t.GetPixels(0));
                Destroy(t);
                t2.Apply(true, true);
                instance.createdObjects.Add(t2);
                newItem.settings_Inventory.Sprite = t2.ToSprite();
                instance.createdObjects.Add(newItem.settings_Inventory.Sprite);
                var cost = newItem.settings_recipe.NewCost.ToList();
                var cm = cost.FirstOrDefault(x => x.items.Any(y => y.UniqueIndex == 23));
                if (cm == null)
                {
                    cm = new CostMultiple(new[] { ItemManager.GetItemByIndex(23) }, 2);
                    cost.Add(cm);
                }
                else
                    cm.amount += 2;
                newItem.settings_recipe.NewCost = cost.ToArray();
                var ps = newItem.settings_buildable.GetBlockPrefabs().ToArray();
                for (int j = 0; j < ps.Length; j++)
                {
                    ps[j] = Instantiate(ps[j], instance.prefabHolder);
                    ps[j].ReplaceValues(baseItem, newItem);
                    for (var p = 0; p < item.Position.Length; p++)
                    {
                        var g = new GameObject("pipeModel");
                        g.transform.SetParent(ps[j].transform, false);
                        g.AddComponent<MeshFilter>().sharedMesh = instance.pipeModel.sharedMesh;
                        g.AddComponent<MeshRenderer>().sharedMaterial = instance.pipeModel.GetComponent<MeshRenderer>().sharedMaterial;
                        g.transform.localPosition = item.Position[p];
                        g.transform.localScale = item.Scale[p];
                    }
                    foreach (var c in ps[j].blockColliders)
                        c.tag = "Pipe";
                }
                Traverse.Create(newItem.settings_buildable).Field("blockPrefabs").SetValue(ps);
            } catch (Exception e)
            {
                Debug.LogError($"[{instance.modlistEntry.jsonmodinfo.name}]: An error occured while creating creating{item.UniqueIndex}\n{e}");
            }
            item.BaseItem = baseItem;
            instance.newItems.Add((baseItem, newItem));
            foreach (var q in Resources.FindObjectsOfTypeAll<SO_BlockQuadType>())
                if (q.AcceptsBlock(baseItem))
                    Traverse.Create(q).Field("acceptableBlockTypes").GetValue<List<Item_Base>>().Add(newItem);
            foreach (var q in Resources.FindObjectsOfTypeAll<SO_BlockCollisionMask>())
                if (q.IgnoresBlock(baseItem))
                    Traverse.Create(q).Field("blockTypesToIgnore").GetValue<List<Item_Base>>().Add(newItem);
            ItemManager.GetAllItems().Add(newItem);
        }
    }

    public class NewItemData
    {
        public int BaseUniqueIndex;
        public Item_Base BaseItem;
        public int UniqueIndex;
        public string UniqueName;
        public string Localization;
        public Vector3[] Position;
        public Vector3[] Scale;
    }

    static class ExtentionMethods
    {
        public static Item_Base Clone(this Item_Base source, int uniqueIndex, string uniqueName)
        {
            Item_Base item = ScriptableObject.CreateInstance<Item_Base>();
            item.Initialize(uniqueIndex, uniqueName, source.MaxUses);
            item.settings_buildable = source.settings_buildable.Clone();
            item.settings_consumeable = source.settings_consumeable.Clone();
            item.settings_cookable = source.settings_cookable.Clone();
            item.settings_equipment = source.settings_equipment.Clone();
            item.settings_Inventory = source.settings_Inventory.Clone();
            item.settings_recipe = source.settings_recipe.Clone();
            item.settings_usable = source.settings_usable.Clone();
            return item;
        }
        public static void SetRecipe(this Item_Base item, CostMultiple[] cost, CraftingCategory category = CraftingCategory.Resources, int amountToCraft = 1, bool learnedFromBeginning = false, string subCategory = null, int subCatergoryOrder = 0)
        {
            Traverse recipe = Traverse.Create(item.settings_recipe);
            recipe.Field("craftingCategory").SetValue(category);
            recipe.Field("amountToCraft").SetValue(amountToCraft);
            recipe.Field("learnedFromBeginning").SetValue(learnedFromBeginning);
            recipe.Field("subCategory").SetValue(subCategory);
            recipe.Field("subCatergoryOrder").SetValue(subCatergoryOrder);
            item.settings_recipe.NewCost = cost;
        }

        public static void CopyFieldsOf(this object value, object source)
        {
            var t1 = value.GetType();
            var t2 = source.GetType();
            while (!t1.IsAssignableFrom(t2))
                t1 = t1.BaseType;
            while (t1 != typeof(Object) && t1 != typeof(object))
            {
                foreach (var f in t1.GetFields(~BindingFlags.Default))
                    if (!f.IsStatic)
                        f.SetValue(value, f.GetValue(source));
                t1 = t1.BaseType;
            }
        }

        public static void ReplaceValues(this Component value, object original, object replacement)
        {
            foreach (var c in value.GetComponentsInChildren<Component>())
                (c as object).ReplaceValues(original, replacement);
        }
        public static void ReplaceValues(this GameObject value, object original, object replacement)
        {
            foreach (var c in value.GetComponentsInChildren<Component>())
                (c as object).ReplaceValues(original, replacement);
        }

        public static void ReplaceValues(this object value, object original, object replacement)
        {
            var t = value.GetType();
            while (t != typeof(Object) && t != typeof(object))
            {
                foreach (var f in t.GetFields(~BindingFlags.Default))
                    if (!f.IsStatic && f.GetValue(value) == original)
                        f.SetValue(value, replacement);
                t = t.BaseType;
            }
        }

        public static bool HasFieldWithValue(this object obj, object value)
        {
            var t = obj.GetType();
            while (t != typeof(Object) && t != typeof(object))
            {
                foreach (var f in t.GetFields(~BindingFlags.Default))
                    if (!f.IsStatic && f.GetValue(obj).Equals(value))
                        return true;
                t = t.BaseType;
            }
            return false;
        }
        public static bool HasFieldValueMatch<T>(this object obj, Predicate<T> predicate)
        {
            var t = obj.GetType();
            while (t != typeof(Object) && t != typeof(object))
            {
                foreach (var f in t.GetFields(~BindingFlags.Default))
                    if (!f.IsStatic && f.GetValue(obj) is T && (predicate == null || predicate((T)f.GetValue(obj))))
                        return true;
                t = t.BaseType;
            }
            return false;
        }

        public static Sprite ToSprite(this Texture2D texture, Rect? rect = null, Vector2? pivot = null) => Sprite.Create(texture, rect ?? new Rect(0, 0, texture.width, texture.height), pivot ?? new Vector2(0.5f, 0.5f));


        public static Texture2D GetReadable(this Texture2D source, Rect? copyArea = null, RenderTextureFormat format = RenderTextureFormat.Default, RenderTextureReadWrite readWrite = RenderTextureReadWrite.Default, TextureFormat? targetFormat = null, bool mipChain = true)
        {
            var temp = RenderTexture.GetTemporary(source.width, source.height, 0, format, readWrite);
            Graphics.Blit(source, temp);
            temp.filterMode = FilterMode.Point;
            var prev = RenderTexture.active;
            RenderTexture.active = temp;
            var area = copyArea ?? new Rect(0, 0, temp.width, temp.height);
            var texture = new Texture2D((int)area.width, (int)area.height, targetFormat ?? TextureFormat.RGBA32, mipChain);
            texture.ReadPixels(area, 0, 0);
            texture.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(temp);
            return texture;
        }
        public static void AddOverlay(this Texture2D baseImg)
        {
            var w = baseImg.width - 1;
            var h = baseImg.height - 1;
            for (var x = 0; x <= w; x++)
                for (var y = 0; y <= h; y++)
                    baseImg.SetPixel(x, y, baseImg.GetPixel(x, y).Overlay(Main.instance.overlay.GetPixelBilinear((float)x / w, (float)y / h)));
            baseImg.Apply();
        }
        public static Color Overlay(this Color a, Color b) {
            if (a.a <= 0)
                return b;
            if (b.a <= 0)
                return a;
            var r = b.a / (b.a + a.a * (1 - b.a));
            float Ratio(float aV, float bV) => bV * r + aV * (1 - r);
            return new Color(Ratio(a.r,b.r), Ratio(a.g, b.g), Ratio(a.b, b.b), b.a + a.a * (1 - b.a));
        }
    }

    [HarmonyPatch(typeof(ItemInstance_Buildable.Upgrade), "GetNewItemFromUpgradeItem")]
    static class Patch_GetUpgradeItem
    {
        static void Postfix(ItemInstance_Buildable.Upgrade __instance, Item_Base buildableItem, ref Item_Base __result)
        {
            if (buildableItem != null && buildableItem.UniqueIndex == Main.instance.pipeUpgrade?.UniqueIndex)
            {
                foreach (var p in Main.instance.newItems)
                    if (p.Item1.settings_buildable.Upgrades == __instance)
                    {
                        __result = p.Item2;
                        return;
                    }
                foreach (var p in Main.instance.newItems)
                    if (p.Item1.settings_buildable.Upgrades?.HasFieldValueMatch<Item_Base>(x => x.settings_buildable.Upgrades == __instance) ?? false)
                    {
                        __result = p.Item2;
                        return;
                    }
            }
        }
    }

    [HarmonyPatch(typeof(BlockCreator), "IsBuildableItemUpgradeItem")]
    static class Patch_BlockCreator
    {
        static void Postfix(Item_Base buildableItem, ref bool __result)
        {
            if (!__result && buildableItem?.UniqueIndex == Main.instance.pipeUpgrade?.UniqueIndex)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(Block), "IsWalkable")]
    static class Patch_Block
    {
        static void Postfix(Block __instance, ref bool __result)
        {
            foreach (var p in Main.instance.newItems)
                if (p.Item2.UniqueIndex == __instance.buildableItem?.UniqueIndex)
                {
                    __result = p.Item1.settings_buildable.GetBlockPrefab(0).IsWalkable();
                    return;
                }
        }
    }
}