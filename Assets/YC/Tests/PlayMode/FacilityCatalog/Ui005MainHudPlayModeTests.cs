using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using YC.Domain.Cards;
using YC.Domain.Rules;
using YC.Domain.State;

namespace YC.Tests.PlayMode
{
    public sealed class Ui005MainHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator BothScenes_UseOneBottomActionAndTabbedFormalEntries()
        {
            foreach (var scene in new[] { "SampleScene", "ThreePlayerScene" })
            {
                yield return ClearLaunchContext();
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                yield return null;
                var hud = Find("YC.Presentation.GameplayInteractionHudView");
                Assert.That(hud, Is.Not.Null, scene);
                var args = new object[] { null };
                Assert.That((bool)hud.GetType().GetMethod("TryValidateConfiguration")
                    .Invoke(hud, args), Is.True, args[0] as string);
                var action = Property(hud, "ActionPanelView");
                var frame = Property(hud, "Frame");
                var compactNavigation = Child(hud.transform, "Compact Region Navigation");
                if (compactNavigation != null && compactNavigation.gameObject.activeInHierarchy)
                {
                    Child(compactNavigation, "Region Button 2").GetComponent<Button>().onClick.Invoke();
                    yield return null;
                }
                var end = (Button)Property(action, "EndRoundButton");
                Assert.That(end, Is.SameAs(Property(frame, "EndActionButton")), scene);
                Assert.That(FirstHit(end.transform as RectTransform), Is.SameAs(end.gameObject), scene);
                var build = (Button)Property(action, "BuildButton");
                var special = (Button)Property(action, "SpecialButton");
                Assert.That(build, Is.Not.Null, scene);
                Assert.That(special, Is.Not.Null, scene);
                Assert.That(special.interactable, Is.False, "尚无独立的特殊行动入口能力时应禁用");

                var tabs = Find("YC.Presentation.UiMainActionTabs");
                Assert.That(tabs, Is.Not.Null, scene);
                var buttons = (Button[])Field(tabs, "tabs");
                var sections = (GameObject[])Field(tabs, "sections");
                Assert.That(buttons, Has.Length.EqualTo(3));
                for (var i = 0; i < 3; i++)
                {
                    Assert.That(FirstHit(buttons[i].transform as RectTransform),
                        Is.SameAs(buttons[i].gameObject), scene + " 页签被装饰层遮挡");
                    buttons[i].onClick.Invoke();
                    Assert.That((int)Property(tabs, "SelectedIndex"), Is.EqualTo(i));
                    for (var j = 0; j < 3; j++)
                        Assert.That(sections[j].activeSelf, Is.EqualTo(j == i),
                            scene + " 页签显示了错误行动组");
                }
                buttons[0].onClick.Invoke();
                Assert.That(build.gameObject.activeInHierarchy, Is.True);
                Assert.That(special.gameObject.activeInHierarchy, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator TabState_DisabledOverridesSelection_AndRetainsSelectionOnExit()
        {
            yield return ClearLaunchContext();
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            var tabs = Find("YC.Presentation.UiMainActionTabs");
            var buttons = (Button[])Field(tabs, "tabs");
            var state = buttons[0].GetComponent(Type.GetType(
                "YC.Presentation.UiMainButtonState, Assembly-CSharp", true));
            var image = buttons[0].GetComponent<Image>();
            var selected = (Sprite)Field(state, "selectedFace");
            var selectedHover = (Sprite)Field(state, "selectedHover");
            var disabled = (Sprite)Field(state, "disabled");
            Assert.That(image.sprite, Is.SameAs(selected));
            var pointer = new PointerEventData(EventSystem.current);
            state.GetType().GetMethod("OnPointerEnter").Invoke(state, new object[] { pointer });
            Assert.That(image.sprite, Is.SameAs(selectedHover));
            state.GetType().GetMethod("OnPointerExit").Invoke(state, new object[] { pointer });
            Assert.That(image.sprite, Is.SameAs(selected));
            state.GetType().GetMethod("SetAvailable").Invoke(state, new object[] { false });
            Assert.That(image.sprite, Is.SameAs(disabled));
            state.GetType().GetMethod("SetAvailable").Invoke(state, new object[] { true });
            Assert.That(image.sprite, Is.SameAs(selected));
        }

        [UnityTest]
        public IEnumerator OpponentProjection_ShowsPublicCountsWithoutCardIdentities()
        {
            yield return ClearLaunchContext();
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            var hud = Find("YC.Presentation.GameplayInteractionHudView");
            var modules = Property(hud, "MainModules");
            var state = new GameState();
            state.Players.Add(new PlayerState { PlayerId = 1, Name = "本机玩家", Color = PlayerColor.Red });
            var opponent = new PlayerState
            {
                PlayerId = 2,
                Name = "一个很长的玩家名字用于检查适配",
                Color = PlayerColor.Blue,
                Score = 123
            };
            opponent.HandCardIds.Add("hidden-character-secret-1");
            opponent.HandCardIds.Add("hidden-character-secret-2");
            opponent.DeclaredCityStyles.Add(new CityStyleDeclarationState
                { CityStyleId = "public-style" });
            state.Players.Add(opponent);
            state.Decks.CharacterDeck.Add("deck-card-1");
            var visible = GameStateViewProjector.Project(state, GameStateViewer.Player(1));
            var sanitized = GameStateViewProjector.ToClientState(visible);
            Assert.That(visible.Players[1].HandCardCount, Is.EqualTo(2));
            Assert.That(visible.Players[1].HandCardIds, Is.Empty);
            Assert.That(sanitized.Players[1].HandCardIds, Is.Empty);
            Assert.That(sanitized.Decks.CharacterDeck, Is.Empty);
            modules.GetType().GetMethod("Render").Invoke(modules,
                new object[] { sanitized, visible, 1, null });
            var names = (Text[])Field(modules, "opponentName");
            var scores = (Text[])Field(modules, "opponentScore");
            var hands = (Text[])Field(modules, "opponentHandCount");
            var styles = (Text[])Field(modules, "opponentStyleCount");
            var deck = (Text)Field(modules, "characterDeckCount");
            Assert.That(names[0].text, Is.EqualTo(opponent.Name));
            Assert.That(scores[0].text, Is.EqualTo("123"));
            Assert.That(hands[0].text, Is.EqualTo("2"));
            Assert.That(styles[0].text, Is.EqualTo("1"));
            Assert.That(deck.text, Is.EqualTo("1"));
            foreach (var label in ((Component)modules).GetComponentsInChildren<Text>(true))
                Assert.That(label.text, Does.Not.Contain("hidden-character-secret"));
        }

        [UnityTest]
        public IEnumerator OverflowHand_NavigationKeepsTheCurrentPageAcrossRenders()
        {
            yield return ClearLaunchContext();
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            var panel = Find("YC.Presentation.CharacterHandPanel");
            var ids = new List<string>(CharacterCardDatabase.GetInitialCardIds(
                new PlayerState { PlayerId = 1, Color = PlayerColor.Red }));
            var secondPlayer = CharacterCardDatabase.GetInitialCardIds(
                new PlayerState { PlayerId = 2, Color = PlayerColor.Blue });
            for (var i = 0; i < 2; i++)
                ids.Add(secondPlayer[i]);
            var itemType = Type.GetType(
                "YC.Presentation.Workflows.CharacterCardHandItemViewModel, YC.Presentation.Workflows", true);
            var cards = Array.CreateInstance(itemType, ids.Count);
            for (var i = 0; i < ids.Count; i++)
                cards.SetValue(Activator.CreateInstance(itemType, ids[i], ids[i], false), i);
            var modelType = Type.GetType(
                "YC.Presentation.Workflows.CharacterCardPanelViewModel, YC.Presentation.Workflows", true);
            var model = Activator.CreateInstance(modelType, cards,
                Array.CreateInstance(itemType, 0), string.Empty, string.Empty,
                false, false, false, false, false, false, string.Empty,
                0, 0, 0, 0, CharacterCardEffectKind.Unsupported,
                CharacterCardEffectKind.Unsupported, string.Empty, string.Empty,
                PlayerColor.Red, string.Empty);
            panel.GetType().GetMethod("Render").Invoke(panel, new object[] { 1, model });
            var next = (Button)Field(panel, "handNextButton");
            var previous = (Button)Field(panel, "handPreviousButton");
            var page = (Text)Field(panel, "handPageText");
            Assert.That(next.gameObject.activeInHierarchy, Is.True);
            Assert.That(next.interactable, Is.True);
            Assert.That(previous.interactable, Is.False);
            Assert.That(page.text, Is.EqualTo("1 / 2"));
            Assert.That(Child(panel.transform, "Hand Card: " + ids[6])
                .GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));
            next.onClick.Invoke();
            Assert.That(page.text, Is.EqualTo("2 / 2"));
            Assert.That(previous.interactable, Is.True);
            Assert.That(next.interactable, Is.False);
            Assert.That(Child(panel.transform, "Hand Card: " + ids[6])
                .GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0f));
            panel.GetType().GetMethod("Render").Invoke(panel, new object[] { 1, model });
            Assert.That(page.text, Is.EqualTo("2 / 2"));
        }

        [UnityTest]
        public IEnumerator BuildButton_OpensAndCancelsTheExistingDraft()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),
                    "--yc-dev-right-card-smoke-no-dialog") < 0)
                Assert.Ignore("此用例使用正式右卡行动阶段启动状态。");
            yield return ClearLaunchContext();
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Single);
            yield return null;
            var controller = Find("YC.Presentation.MobileCityInteractionController");
            var presenter = Field(controller, "turnActionPresenter");
            var coordinator = Field(controller, "buildFacilityInteraction");

            var hud = Find("YC.Presentation.GameplayInteractionHudView");
            var action = Property(hud, "ActionPanelView");
            var build = (Button)Property(action, "BuildButton");
            var panel = Property(hud, "BuildInfoPanel");
            var panelView = Property(panel, "View");
            var group = ((RectTransform)Property(panelView, "Root")).GetComponent<CanvasGroup>();
            Assert.That(build.interactable, Is.True, "建设入口应使用当前主要行动能力");
            Assert.That(group.alpha, Is.EqualTo(0f), "未进入建设时旧供应板不应盖住主模块");
            build.onClick.Invoke();
            var interaction = Property(presenter, "BuildInteraction");
            Assert.That((bool)Property(interaction, "IsActive"), Is.True);
            Assert.That(group.alpha, Is.EqualTo(1f), "建设草稿应显示现有供应交互");
            interaction.GetType().GetMethod("Cancel").Invoke(interaction, null);
            coordinator.GetType().GetMethod("Synchronize").Invoke(coordinator, null);
            Assert.That((bool)Property(interaction, "IsActive"), Is.False);
            Assert.That(group.alpha, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator CityStyleTab_OpensAndClosesTheFormalQuickActionPreview()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),
                    "--yc-dev-right-card-smoke-no-dialog") < 0)
                Assert.Ignore("此用例使用正式右卡行动阶段启动状态。");
            foreach (var scene in new[] { "SampleScene", "ThreePlayerScene" })
            {
                yield return ClearLaunchContext();
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                yield return null;
                var tabs = Find("YC.Presentation.UiMainActionTabs");
                ((Button[])Field(tabs, "tabs"))[2].onClick.Invoke();
                var hud = Find("YC.Presentation.GameplayInteractionHudView");
                var action = Property(hud, "ActionPanelView");
                var declare = (Button)Property(action, "DeclareCityStyleButton");
                Assert.That(declare.gameObject.activeInHierarchy, Is.True, scene);
                Assert.That(declare.interactable, Is.True, scene);
                declare.onClick.Invoke();
                var controller = Find("YC.Presentation.MobileCityInteractionController");
                var workflow = Field(controller, "workflowView");
                var preview = Field(workflow, "cityStyleDeclarationDialog");
                Assert.That((bool)Property(preview, "IsShowing"), Is.True, scene);
                var previewView = Field(preview, "view");
                ((Button)Property(previewView, "CloseButton")).onClick.Invoke();
                Assert.That((bool)Property(preview, "IsShowing"), Is.False, scene);
                Assert.That((int)Property(tabs, "SelectedIndex"), Is.EqualTo(2), scene);
            }
        }

        private static Component Find(string typeName)
        {
            var type = Type.GetType(typeName + ", Assembly-CSharp", true);
            return UnityEngine.Object.FindObjectOfType(type) as Component;
        }

        private static IEnumerator ClearLaunchContext()
        {
            var context = Find("YC.Presentation.GameLaunchContext");
            if (context == null) yield break;
            UnityEngine.Object.Destroy(context.gameObject);
            yield return null;
        }

        private static object Property(object target, string name)
        {
            return target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                .GetValue(target);
        }

        private static object Field(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(target);
        }

        private static GameObject FirstHit(RectTransform target)
        {
            Canvas.ForceUpdateCanvases();
            var point = RectTransformUtility.WorldToScreenPoint(null,
                target.TransformPoint(target.rect.center));
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, results);
            return results.Count == 0 ? null : results[0].gameObject;
        }

        private static Transform Child(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = Child(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
