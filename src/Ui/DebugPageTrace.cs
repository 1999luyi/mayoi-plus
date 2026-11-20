using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pwaat.Accessibility.Ui
{
    internal sealed class DebugPageTrace : IDisposable
    {
        private readonly UiRuntime _runtime;
        private readonly FieldInfo _scenes = UiResources.Field(typeof(titleCtrlRoot), "scene_list_");
        internal DebugPageTrace(UiRuntime runtime) { _runtime = runtime; }
        internal void Install(Harmony harmony)
        {
            int targets = 0;
            foreach (Type type in typeof(sceneCtrl).Assembly.GetTypes())
            {
                if (!typeof(sceneCtrl).IsAssignableFrom(type)) { continue; }
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if ((method.Name != "Play" && method.Name != "End") || method.GetParameters().Length != 0 || method.ReturnType != typeof(void) || method.IsAbstract || method.GetMethodBody() == null) { continue; }
                    NativeHooks.Patch(harmony, method, "PageBefore", "PageAfter", null); targets++;
                }
            }
            NativeHooks.Patch(harmony, AccessTools.Method(typeof(titleCtrlRoot), "Scene", new[] { typeof(titleCtrlRoot.SceneType) }), "SceneRequest", null, null);
            foreach (Type type in new[] { typeof(optionCtrl), typeof(SaveLoadUICtrl), typeof(messageBoxCtrl) })
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.IsAbstract || method.GetMethodBody() == null) { continue; }
                    if (method.Name == "Open" || method.Name == "Close" || method.Name == "OpenWindow" || method.Name == "CloseWindow" || method.Name == "OpenWindowSelect" || method.Name == "CloseWindowSelect") { NativeHooks.Patch(harmony, method, "PageBefore", "PageAfter", null); targets++; }
                    if ((method.Name == "set_is_open" || method.Name == "set_active") && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(bool)) { NativeHooks.Patch(harmony, method, null, "PageFlag", null); targets++; }
                }
            }
            foreach (MethodInfo method in typeof(DialogReleaseNoteCtrl).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.IsAbstract || method.GetMethodBody() == null) { continue; }
                if (method.Name == "Awake" || method.Name == "RegisterDecideEvent") { NativeHooks.Patch(harmony, method, "PageBefore", "PageAfter", null); targets++; }
            }
            foreach (MethodInfo method in typeof(DialogTermsOfServiceCtrl).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.IsAbstract || method.GetMethodBody() == null) { continue; }
                if (method.Name == "Initialize" || method.Name == "playCursor" || method.Name == "stopCursor") { NativeHooks.Patch(harmony, method, "PageBefore", "PageAfter", null); targets++; }
            }
            foreach (Type type in new[] { typeof(GeneralSelectPlateCtrl), typeof(GalleryArtLibraryContentsCtrl), typeof(GalleryActionStudioListWindowCtrl), typeof(ConfirmDialog), typeof(creditCtrl) })
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.IsAbstract || method.GetMethodBody() == null) { continue; }
                    if (method.Name == "Show" || method.Name == "Hide" || method.Name == "Close" || method.Name == "Play" || method.Name == "End" || method.Name == "Stop" || method.Name == "set_IsShow") { NativeHooks.Patch(harmony, method, "PageBefore", "PageAfter", null); targets++; }
                }
            }
            SceneManager.sceneLoaded += UnityLoaded;
            SceneManager.sceneUnloaded += UnityUnloaded;
            _runtime.Trace("page_trace_installed", "nativeTargets=" + targets);
        }
        internal void Call(object instance, MethodBase method, string stage)
        {
            UnityEngine.Object obj = instance as UnityEngine.Object;
            string id = obj != null ? obj.GetInstanceID().ToString() : "<none>";
            sceneCtrl scene = instance as sceneCtrl;
            string body = scene != null && scene.body != null ? scene.body.activeInHierarchy.ToString() : "<not-sceneCtrl>";
            _runtime.Trace("page_lifecycle", "stage=" + stage + " page=" + instance.GetType().FullName + " method=" + method.DeclaringType.FullName + "." + method.Name + " id=" + id + " bodyActive=" + body);
        }
        internal void SceneRequest(titleCtrlRoot root, titleCtrlRoot.SceneType requested)
        {
            List<sceneCtrl> scenes = (List<sceneCtrl>)_scenes.GetValue(root);
            int index = (int)requested;
            string target = index >= 0 && index < scenes.Count && scenes[index] != null ? scenes[index].GetType().FullName : "<no-target>";
            _runtime.Trace("scene_request", "scene=" + requested + " index=" + index + " target=" + target + " root=" + root.GetInstanceID());
        }
        private void UnityLoaded(Scene scene, LoadSceneMode mode) { _runtime.Trace("unity_scene_loaded", "name=" + scene.name + " mode=" + mode); }
        private void UnityUnloaded(Scene scene) { _runtime.Trace("unity_scene_unloaded", "name=" + scene.name); }
        /// <summary>Removes only the subscribed Unity scene notifications.</summary>
        public void Dispose() { SceneManager.sceneLoaded -= UnityLoaded; SceneManager.sceneUnloaded -= UnityUnloaded; }
    }
}
