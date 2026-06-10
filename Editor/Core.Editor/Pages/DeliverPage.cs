#if UNITY_EDITOR
using Pitech.XR.ContentDelivery;
using Pitech.XR.ContentDelivery.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pitech.XR.Core.Editor
{
    // Cockpit page: DELIVER - Addressables / content publishing. Addressables Builder launch tile,
    // the GuidedSetup Addressables card (verbatim), the "Addressables (Project)" config editor moved
    // verbatim as a cohesive STATEFUL unit from the former SettingsPage (instance fields + bind/save
    // lifecycle preserved), and the reserved Analytics module tile (WS A2 Step 4/7).
    public sealed class DeliverPage : IDevkitPage
    {
        readonly AddressablesService addressablesService = new AddressablesService();

        ObjectField addressablesConfigField;
        VisualElement addressablesConfigFieldsRoot;
        SerializedObject addressablesSerializedObject;

        public string Title => "Deliver";

        public void BuildUI(VisualElement root)
        {
            // ===== Workspaces (launch tiles - the Hub launches windows, never re-implements) =====
            {
                var section = DevkitTheme.Section("Workspaces");
                section.Add(DevkitTheme.Body("Content-delivery workspaces. The Hub opens them; it never re-implements them.", dim: true));
                section.Add(DevkitTheme.VSpace(8));
                var grid = DevkitWidgets.TileGrid();
                grid.Add(DevkitWidgets.Card(
                    "Addressables Builder",
                    "Setup, prefab mapping, validate and build Addressables / CCD content in one place.",
                    DevkitWidgets.Actions(DevkitTheme.Primary("Open Addressables Builder", AddressablesBuilderWindow.Open))));
                section.Add(grid);
                root.Add(section);
            }

            // ===== Addressables / CCD scene-setup card (verbatim from GuidedSetupPage) =====
            {
                var section = DevkitTheme.Section("Content Delivery");
                var grid = DevkitWidgets.TileGrid();
                grid.Add(CardAddressables());
                section.Add(grid);
                root.Add(section);
            }

            // ===== Addressables (Project) config editor (verbatim stateful unit from SettingsPage) =====
            root.Add(AddrSection("Addressables (Project)", BuildAddressablesSettingsSection));

            // ===== Reserved module: Analytics (Step 7 -> Deliver tile) =====
            {
                var section = DevkitTheme.Section("Reserved modules");
                var grid = DevkitWidgets.TileGrid();
                grid.Add(ReservedTile(
                    "Analytics",
                    "Action tracking, scoring and portal data. Reserved slot - logic lands Phase B WS B1-B6 (spec §28.5)."));
                section.Add(grid);
                root.Add(section);
            }
        }

        // ----- GuidedSetup Addressables card (verbatim) -----
        static VisualElement CardAddressables()
        {
            bool hasAddr = ContentDeliveryCapability.HasAddressablesPackage;
            bool hasCcd = ContentDeliveryCapability.HasCcdPackage;
            var setupService = new AddressablesService();

            var pills = DevkitWidgets.PillsRow(
                (hasAddr ? DevkitWidgets.PillKind.Success : DevkitWidgets.PillKind.Warning, hasAddr ? "Addressables ready" : "Addressables missing"),
                (hasCcd ? DevkitWidgets.PillKind.Success : DevkitWidgets.PillKind.Neutral, hasCcd ? "CCD package present" : "CCD optional"),
                (DevkitWidgets.PillKind.Neutral, "Content delivery optional")
            );

            var body = new VisualElement();
            body.Add(pills);
            body.Add(DevkitTheme.VSpace(8));
            body.Add(DevkitTheme.Body(
                "Addressables/CCD publishing is handled in a dedicated builder window.\n" +
                "Scene wiring lives on the Author page.",
                dim: true));
            body.Add(DevkitTheme.VSpace(6));
            body.Add(DevkitTheme.Body(
                "Use Addressables Builder for Setup, Prefab mapping, Validate, and Build in one place.",
                dim: true));

            return DevkitWidgets.Card(
                "Addressables / CCD (optional)",
                "Use a dedicated builder window (separate from scene setup).",
                DevkitWidgets.Actions(
                    DevkitTheme.Primary("Open Addressables Builder", () =>
                    {
                        AddressablesBuilderWindow.Open();
                    }),
                    DevkitTheme.Secondary("Ping Module Config", () =>
                    {
                        AddressablesModuleConfig config = setupService.EnsureConfigAsset(out _, out _);
                        if (config != null)
                        {
                            EditorGUIUtility.PingObject(config);
                        }
                    })
                ),
                body
            );
        }

        // ----- "Addressables (Project)" config editor (verbatim stateful unit from SettingsPage) -----
        // Moved as one cohesive unit: the instance fields above + these four methods + the private
        // AddrSection/AddrButton helpers. Behaviour identical to the former SettingsPage.
        private void BuildAddressablesSettingsSection(VisualElement el)
        {
            AddressablesModuleConfig config = addressablesService.EnsureConfigAsset(out _, out _);

            addressablesConfigField = new ObjectField("Module Config")
            {
                objectType = typeof(AddressablesModuleConfig),
                allowSceneObjects = false,
                value = config,
            };
            addressablesConfigField.RegisterValueChangedCallback(evt => BindAddressablesConfig(evt.newValue as AddressablesModuleConfig));
            el.Add(addressablesConfigField);

            el.Add(DevkitTheme.Body(
                "Project-specific Addressables defaults. Setup in Addressables Builder reads these values and applies them to profile paths.",
                dim: true));

            addressablesConfigFieldsRoot = new VisualElement();
            addressablesConfigFieldsRoot.style.marginTop = 6;
            el.Add(addressablesConfigFieldsRoot);

            var actions = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 8,
                    flexWrap = Wrap.Wrap,
                }
            };
            actions.Add(AddrButton("Save Config", SaveAddressablesConfig));
            actions.Add(AddrButton("Ping Config", () =>
            {
                if (addressablesConfigField != null && addressablesConfigField.value != null)
                {
                    EditorGUIUtility.PingObject(addressablesConfigField.value);
                }
            }));
            actions.Add(AddrButton("Open Addressables Builder", AddressablesBuilderWindow.Open));
            el.Add(actions);

            BindAddressablesConfig(config);
        }

        private void BindAddressablesConfig(AddressablesModuleConfig config)
        {
            if (addressablesConfigFieldsRoot == null)
            {
                return;
            }

            addressablesConfigFieldsRoot.Clear();
            addressablesSerializedObject = null;

            if (config == null)
            {
                addressablesConfigFieldsRoot.Add(DevkitTheme.Body("Select an AddressablesModuleConfig asset.", dim: true));
                return;
            }

            addressablesSerializedObject = new SerializedObject(config);

            AddAddressablesProperty("provider");
            AddAddressablesProperty("environment");
            AddAddressablesProperty("catalogMode");
            AddAddressablesProperty("profileName");
            AddAddressablesProperty("groupNameTemplate");
            AddAddressablesProperty("remoteCatalogBaseUrl");
            AddAddressablesProperty("remoteLoadPathTemplate");
            AddAddressablesProperty("ccdRemoteLoadPathTemplate");
            AddAddressablesProperty("remoteCatalogUrlTemplate");
            AddAddressablesProperty("playerVersionOverride");
            AddAddressablesProperty("localWorkspaceRoot");
            AddAddressablesProperty("localReportsFolder");
            AddAddressablesProperty("allowOfflineCacheLaunch");
            AddAddressablesProperty("allowOlderCachedSameLab");
            AddAddressablesProperty("networkRequiredIfCacheMiss");
            AddAddressablesProperty("rewriteRemoteBundleUrls");
            AddAddressablesProperty("adapterTypeName");
        }

        private void AddAddressablesProperty(string propertyName)
        {
            if (addressablesSerializedObject == null || addressablesConfigFieldsRoot == null)
            {
                return;
            }

            SerializedProperty property = addressablesSerializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            var field = new PropertyField(property)
            {
                style =
                {
                    marginBottom = 4,
                }
            };
            field.Bind(addressablesSerializedObject);
            addressablesConfigFieldsRoot.Add(field);
        }

        private void SaveAddressablesConfig()
        {
            if (addressablesSerializedObject == null)
            {
                return;
            }

            addressablesSerializedObject.ApplyModifiedProperties();

            if (addressablesConfigField != null && addressablesConfigField.value is AddressablesModuleConfig cfg)
            {
                EditorUtility.SetDirty(cfg);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Private section/button helpers carried verbatim from SettingsPage, renamed to AddrSection /
        // AddrButton so they never shadow DevkitTheme.Section used by the launcher/reserved sections.
        static VisualElement AddrSection(string title, System.Action<VisualElement> fill)
        {
            var box = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.13f, 0.15f, 0.18f, 1f),
                    paddingTop = 10,
                    paddingBottom = 10,
                    paddingLeft = 10,
                    paddingRight = 10,
                    marginBottom = 10,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                }
            };
            var label = new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } };
            box.Add(label);
            var content = new VisualElement();
            box.Add(content);
            fill?.Invoke(content);
            return box;
        }

        static Button AddrButton(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.marginRight = 6;
            button.style.marginBottom = 6;
            return button;
        }

        // Reserved-module tile: announces a future module. No action, no behaviour (Phase A
        // reserves the slot only). Body carries a Neutral "Reserved" pill.
        static VisualElement ReservedTile(string title, string subtitle) =>
            DevkitWidgets.Card(
                title,
                subtitle,
                DevkitWidgets.Actions(),
                DevkitWidgets.PillsRow((DevkitWidgets.PillKind.Neutral, "Reserved")));
    }
}
#endif
