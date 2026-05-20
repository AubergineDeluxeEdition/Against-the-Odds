using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace AgainstTheOdds.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VideoPlayer))]
    public class CinematicBackgroundPlayer : MonoBehaviour
    {
        public enum BackgroundMode
        {
            StillImage,
            VideoSequence
        }

        [Header("Source")]
        [SerializeField] private BackgroundMode mode = BackgroundMode.StillImage;
        [SerializeField] private Sprite stillImage;
        [SerializeField] private VideoClip[] videoClips;
        [Tooltip("Optional playback speed for each video. Missing or <= 0 entries use 1.")]
        [SerializeField] private float[] videoPlaybackSpeeds;
        [SerializeField] private bool loopLastVideo = true;

        [Header("Camera Fit")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool coverCamera = true;
        [SerializeField] private Vector2 framingOffset;
        [SerializeField] private float distanceFromCamera = 10f;
        [SerializeField] private float scaleMultiplier = 1f;

        [Header("Render")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = -1000;
        [SerializeField] private Material materialOverride;
        [SerializeField] private bool hideUntilVideoPrepared = true;

        [Header("Completion")]
        [SerializeField] private bool continueAfterLastVideo;
        [SerializeField, Min(0f)] private float continueDelay;
        [SerializeField] private CinematicSceneController sceneController;

        private VideoPlayer videoPlayer;
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private Material runtimeMaterial;
        private RenderTexture renderTexture;
        private int currentVideoIndex;
        private float currentAspect = 16f / 9f;
        private bool leavingLoopVideo;
        private bool shouldKeepVideoPlaying;
        private Coroutine playRoutine;
        private Coroutine continueRoutine;

        private void Awake()
        {
            EnsureReferences();
            ConfigureRenderer();
            ConfigureVideoPlayerDefaults();
        }

        private void OnEnable()
        {
            EnsureReferences();
            ConfigureRenderer();
            ConfigureVideoPlayerDefaults();
            ApplyMode();
        }

        private void OnDisable()
        {
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnVideoFinished;
                videoPlayer.Stop();
            }

            shouldKeepVideoPlaying = false;

            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            if (continueRoutine != null)
            {
                StopCoroutine(continueRoutine);
                continueRoutine = null;
            }
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture();

            if (runtimeMaterial != null && runtimeMaterial != materialOverride)
            {
                Destroy(runtimeMaterial);
            }
        }

        private void LateUpdate()
        {
            FitToCamera();
            KeepPreparedVideoPlaying();
        }

        public void PlayFromStart()
        {
            currentVideoIndex = 0;
            ApplyMode();
        }

        public void SetStillImage(Sprite sprite)
        {
            stillImage = sprite;
            mode = BackgroundMode.StillImage;
            ApplyMode();
        }

        public void SetVideoSequence(VideoClip[] clips)
        {
            videoClips = clips;
            mode = BackgroundMode.VideoSequence;
            PlayFromStart();
        }

        public IEnumerator PrepareForSceneReveal()
        {
            EnsureReferences();
            ConfigureRenderer();
            ConfigureVideoPlayerDefaults();
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            if (mode != BackgroundMode.VideoSequence || videoClips == null || videoClips.Length == 0)
            {
                ApplyStillImage();
                yield break;
            }

            currentVideoIndex = Mathf.Clamp(currentVideoIndex, 0, videoClips.Length - 1);
            yield return PlayVideoAtIndex(currentVideoIndex);
        }

        private void EnsureReferences()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            videoPlayer = GetComponent<VideoPlayer>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();

            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            if (meshFilter.sharedMesh == null) meshFilter.sharedMesh = CreateQuadMesh();
        }

        private void ConfigureRenderer()
        {
            if (runtimeMaterial == null)
            {
                runtimeMaterial = materialOverride != null
                    ? new Material(materialOverride)
                    : CreateDefaultMaterial();
            }

            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;
        }

        private void ConfigureVideoPlayerDefaults()
        {
            if (videoPlayer == null) return;

            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        }

        private void ApplyMode()
        {
            if (mode == BackgroundMode.VideoSequence && videoClips != null && videoClips.Length > 0)
            {
                currentVideoIndex = Mathf.Clamp(currentVideoIndex, 0, videoClips.Length - 1);
                if (playRoutine != null)
                {
                    StopCoroutine(playRoutine);
                }

                playRoutine = StartCoroutine(PlayVideoAtIndex(currentVideoIndex));
                return;
            }

            ApplyStillImage();
        }

        private void ApplyStillImage()
        {
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnVideoFinished;
                videoPlayer.Stop();
            }

            shouldKeepVideoPlaying = false;
            ReleaseRenderTexture();

            Texture texture = stillImage != null ? stillImage.texture : Texture2D.blackTexture;
            SetMaterialTexture(texture);
            currentAspect = texture != null && texture.height > 0
                ? (float)texture.width / texture.height
                : 16f / 9f;

            meshRenderer.enabled = true;
            FitToCamera();
        }

        private IEnumerator PlayVideoAtIndex(int index)
        {
            if (videoPlayer == null || videoClips == null || videoClips.Length == 0) yield break;

            VideoClip clip = videoClips[Mathf.Clamp(index, 0, videoClips.Length - 1)];
            if (clip == null) yield break;

            leavingLoopVideo = false;
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.Stop();
            ConfigureVideoPlayerDefaults();
            bool isLastVideo = index == videoClips.Length - 1;
            videoPlayer.isLooping = loopLastVideo && isLastVideo && !continueAfterLastVideo;
            videoPlayer.playbackSpeed = GetPlaybackSpeed(index);
            videoPlayer.clip = clip;

            PrepareRenderTexture(clip);
            videoPlayer.targetTexture = renderTexture;
            SetMaterialTexture(renderTexture);
            currentAspect = clip.height > 0 ? (float)clip.width / clip.height : currentAspect;

            if (hideUntilVideoPrepared)
            {
                meshRenderer.enabled = false;
            }

            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared)
            {
                yield return null;
            }

            FitToCamera();
            meshRenderer.enabled = true;
            videoPlayer.loopPointReached += OnVideoFinished;
            shouldKeepVideoPlaying = true;
            videoPlayer.Play();
            playRoutine = null;
        }

        private void OnVideoFinished(VideoPlayer source)
        {
            if (source == null || leavingLoopVideo) return;
            if (videoClips == null || videoClips.Length == 0) return;
            if (currentVideoIndex >= videoClips.Length - 1)
            {
                shouldKeepVideoPlaying = videoPlayer != null && videoPlayer.isLooping && !continueAfterLastVideo;
                if (continueAfterLastVideo)
                {
                    StartContinueAfterLastVideo();
                }

                return;
            }

            leavingLoopVideo = true;
            shouldKeepVideoPlaying = false;
            currentVideoIndex++;
            playRoutine = StartCoroutine(PlayVideoAtIndex(currentVideoIndex));
        }

        private void KeepPreparedVideoPlaying()
        {
            if (!shouldKeepVideoPlaying || videoPlayer == null) return;
            if (!isActiveAndEnabled || mode != BackgroundMode.VideoSequence) return;
            if (videoPlayer.clip == null || !videoPlayer.isPrepared || videoPlayer.isPlaying) return;
            if (continueRoutine != null || leavingLoopVideo) return;

            videoPlayer.Play();
        }

        private float GetPlaybackSpeed(int index)
        {
            if (videoPlaybackSpeeds == null || index < 0 || index >= videoPlaybackSpeeds.Length)
            {
                return 1f;
            }

            return videoPlaybackSpeeds[index] > 0f ? videoPlaybackSpeeds[index] : 1f;
        }

        private void StartContinueAfterLastVideo()
        {
            if (continueRoutine != null) return;
            continueRoutine = StartCoroutine(ContinueAfterDelay());
        }

        private IEnumerator ContinueAfterDelay()
        {
            if (continueDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(continueDelay);
            }

            if (sceneController == null)
            {
                sceneController = GetComponent<CinematicSceneController>();
            }

            if (sceneController == null)
            {
                sceneController = FindFirstObjectByType<CinematicSceneController>();
            }

            if (sceneController != null)
            {
                sceneController.Continue();
            }
            else
            {
                Debug.LogWarning("[CinematicBackgroundPlayer] Cannot continue after final video: no CinematicSceneController found.");
            }

            continueRoutine = null;
        }

        private void PrepareRenderTexture(VideoClip clip)
        {
            int width = Mathf.Max(16, (int)clip.width);
            int height = Mathf.Max(16, (int)clip.height);

            if (renderTexture != null && renderTexture.width == width && renderTexture.height == height)
            {
                return;
            }

            ReleaseRenderTexture();
            renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = $"{name}_CinematicBackgroundRT",
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture.Create();
        }

        private void ReleaseRenderTexture()
        {
            if (videoPlayer != null && videoPlayer.targetTexture == renderTexture)
            {
                videoPlayer.targetTexture = null;
            }

            if (renderTexture == null) return;

            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }

        private void FitToCamera()
        {
            if (targetCamera == null) return;

            Transform cameraTransform = targetCamera.transform;
            transform.position = cameraTransform.position + cameraTransform.forward * distanceFromCamera
                + cameraTransform.right * framingOffset.x
                + cameraTransform.up * framingOffset.y;
            transform.rotation = cameraTransform.rotation;

            if (!coverCamera) return;

            float worldHeight;
            float worldWidth;
            if (targetCamera.orthographic)
            {
                worldHeight = targetCamera.orthographicSize * 2f;
                worldWidth = worldHeight * targetCamera.aspect;
            }
            else
            {
                worldHeight = 2f * distanceFromCamera * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                worldWidth = worldHeight * targetCamera.aspect;
            }

            float cameraAspect = worldWidth / Mathf.Max(0.0001f, worldHeight);
            float width = worldWidth;
            float height = worldHeight;

            if (currentAspect > cameraAspect)
            {
                width = height * currentAspect;
            }
            else
            {
                height = width / Mathf.Max(0.0001f, currentAspect);
            }

            transform.localScale = new Vector3(width * scaleMultiplier, height * scaleMultiplier, 1f);
        }

        private void SetMaterialTexture(Texture texture)
        {
            if (runtimeMaterial == null) return;

            if (runtimeMaterial.HasProperty("_BaseMap")) runtimeMaterial.SetTexture("_BaseMap", texture);
            if (runtimeMaterial.HasProperty("_MainTex")) runtimeMaterial.SetTexture("_MainTex", texture);
        }

        private static Material CreateDefaultMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            return material;
        }

        private static Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "CinematicBackgroundQuad"
            };

            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
