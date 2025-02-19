﻿namespace StardewMods.BetterChests.Features;

using System;
using System.Collections.Generic;
using System.Linq;
using Common.Helpers;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewMods.BetterChests.Enums;
using StardewMods.BetterChests.Interfaces.Config;
using StardewMods.BetterChests.Models;
using StardewMods.BetterChests.Services;
using StardewMods.FuryCore.Helpers;
using StardewMods.FuryCore.Interfaces;
using StardewMods.FuryCore.Interfaces.MenuComponents;
using StardewMods.FuryCore.Models.CustomEvents;
using StardewMods.FuryCore.UI;
using StardewValley;

/// <inheritdoc />
internal class ChestMenuTabs : Feature
{
    private readonly Lazy<AssetHandler> _assetHandler;
    private readonly PerScreen<object> _context = new();
    private readonly PerScreen<ItemMatcher> _itemMatcher = new(() => new(true));
    private readonly Lazy<IMenuComponents> _menuComponents;
    private readonly Lazy<IMenuItems> _menuItems;
    private readonly PerScreen<int> _tabIndex = new(() => -1);
    private readonly PerScreen<IList<TabComponent>> _tabs = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChestMenuTabs" /> class.
    /// </summary>
    /// <param name="config">Data for player configured mod options.</param>
    /// <param name="helper">SMAPI helper for events, input, and content.</param>
    /// <param name="services">Provides access to internal and external services.</param>
    public ChestMenuTabs(IConfigModel config, IModHelper helper, IModServices services)
        : base(config, helper, services)
    {
        this._assetHandler = services.Lazy<AssetHandler>();
        this._menuComponents = services.Lazy<IMenuComponents>();
        this._menuItems = services.Lazy<IMenuItems>();
    }

    private AssetHandler Assets
    {
        get => this._assetHandler.Value;
    }

    private object Context
    {
        get => this._context.Value;
        set => this._context.Value = value;
    }

    private int Index
    {
        get => this._tabIndex.Value;
        set => this._tabIndex.Value = value;
    }

    private ItemMatcher ItemMatcher
    {
        get => this._itemMatcher.Value;
    }

    private IMenuComponents MenuComponents
    {
        get => this._menuComponents.Value;
    }

    private IMenuItems MenuItems
    {
        get => this._menuItems.Value;
    }

    private IList<TabComponent> Tabs
    {
        get => this._tabs.Value ??= (
                from tab in this.Assets.TabData
                select new TabComponent(
                    new(
                        new(0, 0, 16 * Game1.pixelZoom, 16 * Game1.pixelZoom),
                        this.Helper.Content.Load<Texture2D>(tab.Value[1], ContentSource.GameContent),
                        new(16 * int.Parse(tab.Value[2]), 0, 16, 16),
                        Game1.pixelZoom)
                    {
                        hoverText = tab.Value[0],
                        name = tab.Key,
                    },
                    tab.Value[3].Split(' ')))
            .ToList();
    }

    /// <inheritdoc />
    protected override void Activate()
    {
        this.CustomEvents.ItemGrabMenuChanged += this.OnItemGrabMenuChanged;
        this.CustomEvents.MenuComponentPressed += this.OnMenuComponentPressed;
        this.Helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
        this.Helper.Events.Input.MouseWheelScrolled += this.OnMouseWheelScrolled;
    }

    /// <inheritdoc />
    protected override void Deactivate()
    {
        this.CustomEvents.ItemGrabMenuChanged -= this.OnItemGrabMenuChanged;
        this.CustomEvents.MenuComponentPressed -= this.OnMenuComponentPressed;
        this.Helper.Events.Input.ButtonsChanged -= this.OnButtonsChanged;
        this.Helper.Events.Input.MouseWheelScrolled -= this.OnMouseWheelScrolled;
    }

    private void OnButtonsChanged(object sender, ButtonsChangedEventArgs e)
    {
        if (this.MenuComponents.Menu is null)
        {
            return;
        }

        if (this.Config.ControlScheme.NextTab.JustPressed())
        {
            this.SetTab(this.Index == this.Tabs.Count - 1 ? -1 : this.Index + 1);
            this.Helper.Input.SuppressActiveKeybinds(this.Config.ControlScheme.NextTab);
            return;
        }

        if (this.Config.ControlScheme.PreviousTab.JustPressed())
        {
            this.SetTab(this.Index == -1 ? this.Tabs.Count - 1 : this.Index - 1);
            this.Helper.Input.SuppressActiveKeybinds(this.Config.ControlScheme.PreviousTab);
        }
    }

    private void OnItemGrabMenuChanged(object sender, ItemGrabMenuChangedEventArgs e)
    {
        IStorageData storageData = e.ItemGrabMenu switch
        {
            ItemSelectionMenu when this.Config.DefaultChest.ChestMenuTabs == FeatureOption.Enabled => this.Config.DefaultChest,
            not null when e.Context is not null && this.ManagedObjects.FindManagedStorage(e.Context, out var managedStorage) && managedStorage.ChestMenuTabs == FeatureOption.Enabled => managedStorage,
            _ => null,
        };

        if (storageData is null)
        {
            return;
        }

        // Add filter to Menu Items
        if (this.MenuItems.Menu is not null)
        {
            this.MenuItems.AddFilter(this.ItemMatcher);
        }

        if (this.MenuComponents.Menu is not null)
        {
            var tabs = (
                from tabSet in storageData.ChestMenuTabSet.Select((name, index) => (name, index))
                join tabData in this.Tabs on tabSet.name equals tabData.Name
                orderby tabSet.index
                select tabData).ToList();
            this.MenuComponents.Components.AddRange(tabs.Any() ? tabs : this.Tabs);

            if (!ReferenceEquals(e.Context, this.Context))
            {
                this.Context = e.Context;
                this.SetTab(-1);
            }
        }
    }

    private void OnMenuComponentPressed(object sender, MenuComponentPressedEventArgs e)
    {
        if (e.Component is not TabComponent tab)
        {
            return;
        }

        var index = this.Tabs.IndexOf(tab);
        if (index == -1)
        {
            return;
        }

        this.SetTab(this.Index == index ? -1 : index);
    }

    private void OnMouseWheelScrolled(object sender, MouseWheelScrolledEventArgs e)
    {
        if (this.MenuComponents.Menu is null)
        {
            return;
        }

        var (x, y) = Game1.getMousePosition(true);
        if (!this.Tabs.Any(tab => tab.Component.containsPoint(x, y)))
        {
            return;
        }

        switch (e.Delta)
        {
            case > 0:
                this.SetTab(this.Index == -1 ? this.Tabs.Count - 1 : this.Index - 1);
                break;
            case < 0:
                this.SetTab(this.Index == this.Tabs.Count - 1 ? -1 : this.Index + 1);
                break;
            default:
                return;
        }
    }

    private void SetTab(int index)
    {
        if (this.Index != -1)
        {
            this.Tabs[this.Index].Selected = false;
        }

        this.Index = index;
        if (this.Index != -1)
        {
            Log.Trace($"Switching to Tab {this.Tabs[this.Index].Name}.");
            this.Tabs[this.Index].Selected = true;
            if (this.MenuComponents.Menu.currentlySnappedComponent is not null && Game1.options.SnappyMenus)
            {
                this.MenuComponents.Menu.setCurrentlySnappedComponentTo(this.Tabs[this.Index].Id);
                this.MenuComponents.Menu.snapCursorToCurrentSnappedComponent();
            }
        }

        this.ItemMatcher.Clear();
        if (index != -1)
        {
            foreach (var tag in this.Tabs[this.Index].Tags)
            {
                this.ItemMatcher.Add(tag);
            }
        }
    }
}