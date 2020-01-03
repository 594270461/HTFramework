using UnityEngine;

namespace HT.Framework
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-800)]
    public sealed class HighlightingEffect : MonoBehaviour
    {
        #region Static Fields
        /// <summary>
        /// ������Ⱦ�¼�
        /// </summary>
        internal static event HTFAction<bool, bool> HighlightingEvent;
        
        private static Shader _blurShader;
        /// <summary>
        /// ģ�� Shader
        /// </summary>
        private static Shader BlurShader
        {
            get
            {
                if (_blurShader == null)
                {
                    _blurShader = Shader.Find("Hidden/Highlighted/Blur");
                }
                return _blurShader;
            }
        }

        private static Shader _compositeShader;
        /// <summary>
        /// �ϳ� Shader
        /// </summary>
        private static Shader CompositeShader
        {
            get
            {
                if (_compositeShader == null)
                {
                    _compositeShader = Shader.Find("Hidden/Highlighted/Composite");
                }
                return _compositeShader;
            }
        }

        private static Material _blurMaterial = null;
        /// <summary>
        /// ģ�� Material
        /// </summary>
        private static Material BlurMaterial
        {
            get
            {
                if (_blurMaterial == null)
                {
                    _blurMaterial = new Material(BlurShader);
                    _blurMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                return _blurMaterial;
            }
        }

        private static Material _compositeMaterial = null;
        /// <summary>
        /// �ϳ� Material
        /// </summary>
        private static Material CompositeMaterial
        {
            get
            {
                if (_compositeMaterial == null)
                {
                    _compositeMaterial = new Material(CompositeShader);
                    _compositeMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                return _compositeMaterial;
            }
        }
        #endregion

        #region Public Fields
        //Z�������
        public int StencilZBufferDepth = 0;
        //��������
        public int DownSampleFactor = 4;
        //ģ����������
        public int BlurIterations = 2;
        //ģ����С��ɢֵ
        public float BlurMinSpread = 0.65f;
        //ģ����ɢֵ
        public float BlurSpread = 0.25f;
        //���ʵ�ģ��ǿ��
        public float BlurIntensity = 0.3f;

#if UNITY_EDITOR
        /// <summary>
        /// �Ƿ�����Z�������
        /// </summary>
        public bool StencilZBufferEnabled
        {
            get
            {
                return StencilZBufferDepth > 0;
            }
            set
            {
                if (StencilZBufferEnabled != value)
                {
                    StencilZBufferDepth = value ? 16 : 0;
                }
            }
        }

        /// <summary>
        /// ��������
        /// </summary>
        public int DownSampleFactorProperty
        {
            get
            {
                if (DownSampleFactor == 1)
                {
                    return 0;
                }
                else if (DownSampleFactor == 2)
                {
                    return 1;
                }
                else
                {
                    return 2;
                }
            }
            set
            {
                if (value == 0)
                {
                    DownSampleFactor = 1;
                }
                if (value == 1)
                {
                    DownSampleFactor = 2;
                }
                if (value == 2)
                {
                    DownSampleFactor = 4;
                }
            }
        }

        /// <summary>
        /// ���ʵ�ģ��ǿ��
        /// </summary>
        public float BlurIntensityProperty
        {
            get
            {
                return BlurIntensity;
            }
            set
            {
                if (BlurIntensity != value)
                {
                    BlurIntensity = value;

                    if (Application.isPlaying)
                    {
                        BlurMaterial.SetFloat("_Intensity", BlurIntensity);
                    }
                }
            }
        }
#endif
        #endregion

        #region Private Fields
        //���������������
        private int _layerMask = 1 << HighlightableObject.HighlightingLayer;
        //������Ⱦ�Ļ������������
        private GameObject _shaderCameraObject = null;
        //������Ⱦ�Ļ��������
        private Camera _shaderCamera = null;
        //ģ�建�����Ⱦ����
        private RenderTexture _stencilBuffer = null;
        //������Ⱦ�����
        private Camera _camera = null;
        #endregion
        
        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Start()
        {
            //��֧�ֺ�����Ч
            if (!SystemInfo.supportsImageEffects)
            {
                GlobalTools.LogWarning("HighlightingSystem : Image effects is not supported on this platform! Disabling.");
                enabled = false;
                return;
            }

            //��֧����Ⱦ�����ʽ
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32))
            {
                GlobalTools.LogWarning("HighlightingSystem : RenderTextureFormat.ARGB32 is not supported on this platform! Disabling.");
                enabled = false;
                return;
            }

            //��֧��Highlighting Stencil��ɫ��
            if (!Shader.Find("Hidden/Highlighted/StencilOpaque").isSupported)
            {
                GlobalTools.LogWarning("HighlightingSystem : HighlightingStencilOpaque shader is not supported on this platform! Disabling.");
                enabled = false;
                return;
            }

            //��֧��Highlighting StencilTransparent��ɫ��
            if (!Shader.Find("Hidden/Highlighted/StencilTransparent").isSupported)
            {
                GlobalTools.LogWarning("HighlightingSystem : HighlightingStencilTransparent shader is not supported on this platform! Disabling.");
                enabled = false;
                return;
            }

            //��֧��Highlighting StencilZ��ɫ��
            if (!Shader.Find("Hidden/Highlighted/StencilOpaqueZ").isSupported)
            {
                GlobalTools.LogWarning("HighlightingSystem : HighlightingStencilOpaqueZ shader is not supported on this platform! Disabling.");
                enabled = false;
                return;
            }

            //��֧��Highlighting StencilTransparentZ��ɫ��
            if (!Shader.Find("Hidden/Highlighted/StencilTransparentZ").isSupported)
            {
                GlobalTools.LogWarning("HighlightingSystem : HighlightingStencilTransparentZ shader is not supported on this platform! Disabling.");
                enabled = false;
                return;
            }

            //��֧��HighlightingBlur��ɫ��
            if (!BlurShader.isSupported)
            {
                GlobalTools.LogWarning("HighlightingSystem : HighlightingBlur shader is not supported on this platform! Disabling.");
                enabled = false;
                return;
            }

            //��֧��HighlightingComposite��ɫ��
            if (!CompositeShader.isSupported)
            {
                GlobalTools.LogWarning("HighlightingSystem : HighlightingComposite shader is not supported on this platform! Disabling.");
                enabled = false;
                return;
            }

            BlurMaterial.SetFloat("_Intensity", BlurIntensity);
        }

        private void OnDisable()
        {
            if (_shaderCameraObject != null)
            {
                DestroyImmediate(_shaderCameraObject);
            }

            if (_blurShader)
            {
                _blurShader = null;
            }

            if (_compositeShader)
            {
                _compositeShader = null;
            }

            if (_blurMaterial)
            {
                DestroyImmediate(_blurMaterial);
            }

            if (_compositeMaterial)
            {
                DestroyImmediate(_compositeMaterial);
            }

            if (_stencilBuffer != null)
            {
                RenderTexture.ReleaseTemporary(_stencilBuffer);
                _stencilBuffer = null;
            }
        }

        private void FourTapCone(RenderTexture source, RenderTexture dest, int iteration)
        {
            float off = BlurMinSpread + iteration * BlurSpread;
            BlurMaterial.SetFloat("_OffsetScale", off);
            Graphics.Blit(source, dest, BlurMaterial);
        }

        private void DownSample4x(RenderTexture source, RenderTexture dest)
        {
            float off = 1.0f;
            BlurMaterial.SetFloat("_OffsetScale", off);
            Graphics.Blit(source, dest, BlurMaterial);
        }

        private void OnPreRender()
        {
#if UNITY_4_0
            if (enabled == false || gameObject.activeInHierarchy == false)
#else
            if (enabled == false || gameObject.activeSelf == false)
#endif
                return;

            if (_stencilBuffer != null)
            {
                RenderTexture.ReleaseTemporary(_stencilBuffer);
                _stencilBuffer = null;
            }

            //������Ⱦ
            if (HighlightingEvent != null)
            {
                HighlightingEvent(true, StencilZBufferDepth > 0);
            }
            else
            {
                return;
            }

            _stencilBuffer = RenderTexture.GetTemporary(_camera.pixelWidth, _camera.pixelHeight, StencilZBufferDepth, RenderTextureFormat.ARGB32);

            if (!_shaderCameraObject)
            {
                _shaderCameraObject = new GameObject("HighlightingCamera", typeof(Camera));
                _shaderCameraObject.GetComponent<Camera>().enabled = false;
                _shaderCameraObject.hideFlags = HideFlags.HideAndDontSave;
            }

            if (!_shaderCamera)
            {
                _shaderCamera = _shaderCameraObject.GetComponent<Camera>();
            }

            _shaderCamera.CopyFrom(_camera);
            //_shaderCamera.projectionMatrix = _camera.projectionMatrix;
            _shaderCamera.cullingMask = _layerMask;
            _shaderCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _shaderCamera.renderingPath = RenderingPath.VertexLit;
            _shaderCamera.allowHDR = false;
            _shaderCamera.useOcclusionCulling = false;
            _shaderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _shaderCamera.clearFlags = CameraClearFlags.SolidColor;
            _shaderCamera.targetTexture = _stencilBuffer;
            _shaderCamera.Render();

            //�ر���Ⱦ
            HighlightingEvent?.Invoke(false, false);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_stencilBuffer == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            //��������������ģ��ͼ��
            int width = source.width / DownSampleFactor;
            int height = source.height / DownSampleFactor;
            RenderTexture buffer1 = RenderTexture.GetTemporary(width, height, StencilZBufferDepth, RenderTextureFormat.ARGB32);
            RenderTexture buffer2 = RenderTexture.GetTemporary(width, height, StencilZBufferDepth, RenderTextureFormat.ARGB32);

            //��������4x4��С����
            DownSample4x(_stencilBuffer, buffer1);

            //ģ��С����
            bool oddEven = true;
            for (int i = 0; i < BlurIterations; i++)
            {
                if (oddEven)
                {
                    FourTapCone(buffer1, buffer2, i);
                }
                else
                {
                    FourTapCone(buffer2, buffer1, i);
                }

                oddEven = !oddEven;
            }

            //�ϳ�
            CompositeMaterial.SetTexture("_StencilTex", _stencilBuffer);
            CompositeMaterial.SetTexture("_BlurTex", oddEven ? buffer1 : buffer2);
            Graphics.Blit(source, destination, CompositeMaterial);

            //����
            RenderTexture.ReleaseTemporary(buffer1);
            RenderTexture.ReleaseTemporary(buffer2);
            if (_stencilBuffer != null)
            {
                RenderTexture.ReleaseTemporary(_stencilBuffer);
                _stencilBuffer = null;
            }
        }
    }
}