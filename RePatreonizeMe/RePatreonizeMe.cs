﻿using System;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using HarmonyLib;
using ResoniteModLoader;
using Elements.Core;
using System.Collections.Generic;

namespace RePatreonizeMe
{
    public class RePatreonizeMe : ResoniteMod
    {
        public override string Name => "Re-Patreonize Me";
        public override string Author => "Rucio";
        public override string Version => "0.0.4";
        public override string Link => "https://github.com/bontebok/Re-Patreonize-Me";

        public enum SupporterBadge
        {
            OldPatreonLogo,
            NewPatreonLogo,
            ResoniteSupporter
        }

        private static readonly Dictionary<SupporterBadge, Uri> SupporterBadgeUri = new()
        {
            { SupporterBadge.OldPatreonLogo, new("resdb:///2f6fbe2c14f58f5d3d37607b64319b5712242a05db8f35e773c0bacf8c8e9bac") },
            { SupporterBadge.NewPatreonLogo, new("resdb:///f43ee24a2d141debb36fe2477b7a28f0c39c2c42b17962830709e7ef3b0304ec") },
            { SupporterBadge.ResoniteSupporter, new("resdb:///ac586a5995e02cd4fd49091afd7d437715a53749127b1145fbded5ccdc850f2a") }
        };

        private static Uri GetSupporterBadge(SupporterBadge badge)
        {
            return SupporterBadgeUri[badge];
        }

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<SupporterBadge> BADGE_CHOICE = new("supporterBadge", "Supporter Badge: Select which supporter badge you'd like to use.", () => SupporterBadge.OldPatreonLogo);

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<colorX> LOGO_COLOR = new("logoColor", "Tint Color: Customize the tint color of the badge.", () => new colorX(1f, 0.25882352941f, 0.30196078431f));

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<Uri> CUSTOM_IMAGE_URI = new("customImageUri", "Custom Image: resdb/http/https uri to a custom image. (128x128 resolution).", () => null);

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> DISABLEMOD = new("disableMod", "Disable Mod: Do not change supporter badge.", () => false);

        public static ModConfiguration Config;


        public override void OnEngineInit()
        {
            try
            {
                Config = GetConfiguration();
                Harmony harmony = new("com.Rucio.Re-Patreonize Me");
                harmony.PatchAll();
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        public class RePatreonizeMePatches
        {
            private static void AddPatreonBadge(Slot SupporterSlot)
            {
                Uri logoUri = Config.GetValue(CUSTOM_IMAGE_URI);

                if (!(logoUri != null && (logoUri.Scheme == "resdb" || logoUri.Scheme == "http" || logoUri.Scheme == "https")))
                    logoUri = GetSupporterBadge(Config.GetValue(BADGE_CHOICE));

                SupporterSlot.StartTask(async () =>
                {
                    await new NextUpdate();

                    var attachedModel = SupporterSlot.AttachMesh<QuadMesh, UnlitMaterial>();
                    StaticTexture2D staticTexture2D = SupporterSlot.AttachTexture(logoUri);
                    staticTexture2D.WrapMode = TextureWrapMode.Clamp;
                    staticTexture2D.FilterMode.Value = TextureFilterMode.Bilinear;
                    staticTexture2D.MaxSize.Value = 128;
                    attachedModel.material.Texture.Target = staticTexture2D;
                    attachedModel.material.BlendMode.Value = BlendMode.Alpha;
                    attachedModel.material.TintColor.Value = Config.GetValue(LOGO_COLOR);
                });
            }

            private static void OnSupporterMeshAdded(Slot slot, Slot child)
            {
                if (child.Name == "Supporter")
                {
                    slot.StartTask(async () =>
                    {
                        await new NextUpdate();

                        child.Destroy(); // Destroy Supporter Mesh
                    });
                }
            }

            private static void OnSupporterBadgeAdded(Slot slot, Slot child)
            {
                if (child.Name == "Supporter")
                {
                    if (slot != null)
                        slot.ChildAdded -= OnSupporterBadgeAdded; // Remove listening event

                    var SupporterBadge = child.FindChild("Supporter");

                    if (SupporterBadge == null)
                        child.ChildAdded += OnSupporterMeshAdded;
                    else
                        OnSupporterMeshAdded(child, SupporterBadge);

                    AddPatreonBadge(child);
                    child.Name = "Patreon Supporter";
                }
            }

            private static void OnBadgesAdded(Slot slot, Slot child)
            {
                var SupporterSlot = child.FindChild("Supporter");

                if (SupporterSlot == null)
                {
                    child.ChildAdded += OnSupporterBadgeAdded;
                    return;
                }

                OnSupporterBadgeAdded(null, SupporterSlot);
            }

            [HarmonyPatch(typeof(AvatarBadgeManager))]
            public class AvatarBadgeManagerPatch
            {
                [HarmonyPostfix]
                [HarmonyPatch("OnAwake")]
                public static void AvatarBadgeManagerOnAwakePostfix(AvatarBadgeManager __instance, SyncRef<Slot> ____badgesRoot)
                {
                    __instance.Slot.RunInUpdates(60, () =>
                    {
                        if (Config.GetValue(DISABLEMOD))
                            return;

                        if (!__instance.IsUnderLocalUser) // Should only apply to local user
                            return;

                        var BadgesRoot = ____badgesRoot.Target;

                        if (BadgesRoot == null)
                        {
                            return;
                        }

                        OnBadgesAdded(__instance.Slot, BadgesRoot);
                    });
                }
            }
        }
    }
}