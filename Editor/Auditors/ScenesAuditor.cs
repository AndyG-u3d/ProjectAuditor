using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Unity.ProjectAuditor.Editor.Auditors
{
    public enum SceneProperty
    {
        NumObjects = 0,
        NumUniquePrefabs,
        NumMaterials,
        NumShaders,
        NumTextures,
        Num
    }

    struct SceneStats
    {
        public int objects;
        public int uniquePrefabs;
        public int materials;
        public int shaders;
        public int textures;
    }

    class SceneStatsCollector
    {
        private SceneStats m_Stats;
        readonly Dictionary<UnityEngine.Material, int> m_Materials = new Dictionary<UnityEngine.Material, int>();
        readonly Dictionary<UnityEngine.Object, int> m_Prefabs = new Dictionary<UnityEngine.Object, int>();
        readonly Dictionary<UnityEngine.Shader, int> m_Shaders = new Dictionary<UnityEngine.Shader, int>();
        readonly Dictionary<UnityEngine.Texture, int> m_Textures = new Dictionary<UnityEngine.Texture, int>();

        public void Collect(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                Collect(go);
            }

            foreach (var pair in m_Materials)
            {
                var material = pair.Key;
                var shader = pair.Key.shader;
                if (shader == null)
                    continue;

                if (!m_Shaders.ContainsKey(shader))
                    m_Shaders.Add(shader, 0);

                m_Shaders[shader]++;
#if UNITY_2019_3_OR_NEWER
                for (int i = 0; i < shader.GetPropertyCount(); i++)
                {
                    if (shader.GetPropertyType(i) == ShaderPropertyType.Texture)
                    {
                        var texture = material.GetTexture(shader.GetPropertyName(i));
                        if (texture == null)
                            continue;

                        if (!m_Textures.ContainsKey(texture))
                            m_Textures.Add(texture, 0);

                        m_Textures[texture]++;
                    }
                }
#endif
            }

            m_Stats.materials = m_Materials.Count;
            m_Stats.shaders = m_Shaders.Count;
            m_Stats.textures = m_Textures.Count;
            m_Stats.uniquePrefabs = m_Prefabs.Count;
        }

        void Collect(GameObject go)
        {
            m_Stats.objects++;
            var renderers = go.GetComponents<Renderer>();
            foreach (var material in renderers.SelectMany(r => r.sharedMaterials))
            {
                if (material == null)
                    continue;

                if (!m_Materials.ContainsKey(material))
                    m_Materials.Add(material, 0);
                m_Materials[material]++;
            }

            if (PrefabUtility.GetPrefabInstanceStatus(go) != PrefabInstanceStatus.NotAPrefab)
            {
                var prefab = PrefabUtility.GetPrefabInstanceHandle(go);
                if (!m_Prefabs.ContainsKey(prefab))
                    m_Prefabs.Add(prefab, 0);

                m_Prefabs[prefab]++;
            }
            foreach (Transform childTransform in go.transform)
            {
                Collect(childTransform.gameObject);
            }
        }

        public SceneStats GetStats()
        {
            return m_Stats;
        }
    }

    class ScenesAuditor : IAuditor
    {
        static readonly ProblemDescriptor k_Descriptor = new ProblemDescriptor
            (
            700002,
            "Scene Stats"
            );

        static readonly IssueLayout k_IssueLayout = new IssueLayout
        {
            category = IssueCategory.Scenes,
            properties = new[]
            {
                new PropertyDefinition { type = PropertyType.Description, name = "Scene Name"},
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(SceneProperty.NumObjects), format = PropertyFormat.Integer, name = "Num Objects"},
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(SceneProperty.NumUniquePrefabs), format = PropertyFormat.Integer, name = "Num Unique Prefabs"},
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(SceneProperty.NumMaterials), format = PropertyFormat.Integer, name = "Num Materials"},
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(SceneProperty.NumMaterials), format = PropertyFormat.Integer, name = "Num Shaders"},
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(SceneProperty.NumTextures), format = PropertyFormat.Integer, name = "Num Textures"},
            }
        };

        public IEnumerable<ProblemDescriptor> GetDescriptors()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IssueLayout> GetLayouts()
        {
            yield return k_IssueLayout;
        }

        public void Initialize(ProjectAuditorConfig config)
        {
        }

        public bool IsSupported()
        {
            return true;
        }

        public void RegisterDescriptor(ProblemDescriptor descriptor)
        {
            throw new NotImplementedException();
        }

        public void Audit(Action<ProjectIssue> onIssueFound, Action onComplete = null, IProgressBar progressBar = null)
        {
            foreach (var editorBuildSettingsScene in EditorBuildSettings.scenes)
            {
                var path = editorBuildSettingsScene.path;

                if (!File.Exists(path))
                    continue;

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var collector = new SceneStatsCollector();

                collector.Collect(scene);

                var stats = collector.GetStats();
                onIssueFound(new ProjectIssue(
                    k_Descriptor,
                    path,
                    IssueCategory.Scenes,
                    path,
                    new[]
                    {
                        stats.objects.ToString(),
                        stats.uniquePrefabs.ToString(),
                        stats.materials.ToString(),
                        stats.shaders.ToString(),
                        stats.textures.ToString(),
                    }));
            }

            if (onComplete != null)
                onComplete();
        }
    }
}
