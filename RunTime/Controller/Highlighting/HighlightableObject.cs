using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HT.Framework
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-700)]
    internal sealed class HighlightableObject : MonoBehaviour
    {
        #region Static Fields
        //�����������ڵĲ�
        public static int HighlightingLayer = 7;
        //���������ٶ�
        private static float ConstantOnSpeed = 4.5f;
        //�����ر��ٶ�
        private static float ConstantOffSpeed = 4f;
        //Ĭ�ϼ���ֵ����û�м������Ե���ɫ��
        private static float TransparentCutoff = 0.5f;
        #endregion

        #region Private Fields
        //2����PIֵ
        private const float DoublePI = 2f * Mathf.PI;

        //���еĻ������
        private List<HighlightingRendererCache> _highlightableRenderers;

        //���еĻ��������
        private int[] _layersCache;

        //�����Ƿ����޸�
        private bool _materialsIsDirty = true;

        //��ǰ�Ƿ��Ǹ���״̬
        private bool _currentHighlightingState = false;

        //��ǰ������ɫ
        private Color _currentHighlightingColor;

        //�Ƿ�����ת��
        private bool _transitionActive = false;

        //ת��ֵ
        private float _transitionValue = 0f;
        
        //�Ƿ�ֻ����һ֡
        private bool _isOnce = false;

        //����һ֡����ɫ
        private Color _onceColor = Color.red;

        //�Ƿ���������
        private bool _isFlashing = false;

        //����Ƶ��
        private float _flashingFrequency = 2f;

        //���⿪ʼ��ɫ
        private Color _flashingColorMin = new Color(0.0f, 1.0f, 1.0f, 0.0f);

        //���������ɫ
        private Color _flashingColorMax = new Color(0.0f, 1.0f, 1.0f, 1.0f);

        //�Ƿ��ǳ�������
        private bool _isConstantly = false;

        //����������ɫ
        private Color _constantColor = Color.yellow;

        //�Ƿ������ڹ��
        private bool _isOccluder = false;

        //�Ƿ�������Ȼ���
        private bool _isZWrite = false;

        //�ڹ����ɫ
        private readonly Color _occluderColor = new Color(0.0f, 0.0f, 0.0f, 0.005f);

        //��������
        private Material _highlightingMaterial
        {
            get
            {
                return _isZWrite ? opaqueZMaterial : opaqueMaterial;
            }
        }

        private Material _opaqueMaterial;
        private Material opaqueMaterial
        {
            get
            {
                if (_opaqueMaterial == null)
                {
                    _opaqueMaterial = new Material(opaqueShader);
                    _opaqueMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                return _opaqueMaterial;
            }
        }

        private Material _opaqueZMaterial;
        private Material opaqueZMaterial
        {
            get
            {
                if (_opaqueZMaterial == null)
                {
                    _opaqueZMaterial = new Material(opaqueZShader);
                    _opaqueZMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                return _opaqueZMaterial;
            }
        }

        private static Shader _opaqueShader;
        private static Shader opaqueShader
        {
            get
            {
                if (_opaqueShader == null)
                {
                    _opaqueShader = Shader.Find("Hidden/Highlighted/StencilOpaque");
                }
                return _opaqueShader;
            }
        }

        private static Shader _transparentShader;
        private static Shader transparentShader
        {
            get
            {
                if (_transparentShader == null)
                {
                    _transparentShader = Shader.Find("Hidden/Highlighted/StencilTransparent");
                }
                return _transparentShader;
            }
        }

        private static Shader _opaqueZShader;
        private static Shader opaqueZShader
        {
            get
            {
                if (_opaqueZShader == null)
                {
                    _opaqueZShader = Shader.Find("Hidden/Highlighted/StencilOpaqueZ");
                }
                return _opaqueZShader;
            }
        }

        private static Shader _transparentZShader;
        private static Shader transparentZShader
        {
            get
            {
                if (_transparentZShader == null)
                {
                    _transparentZShader = Shader.Find("Hidden/Highlighted/StencilTransparentZ");
                }
                return _transparentZShader;
            }
        }
        #endregion

        #region Common
        private class HighlightingRendererCache
        {
            public Renderer RendererCached;
            public GameObject GameObjectCached;

            private Material[] _sourceMaterials;
            private Material[] _replacementMaterials;
            private List<int> _transparentMaterialIndexes;

            public HighlightingRendererCache(Renderer renderer, Material[] sourceMaterials, Material sharedOpaqueMaterial, bool writeDepth)
            {
                RendererCached = renderer;
                GameObjectCached = renderer.gameObject;
                _sourceMaterials = sourceMaterials;
                _replacementMaterials = new Material[sourceMaterials.Length];
                _transparentMaterialIndexes = new List<int>();

                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    Material sourceMaterial = sourceMaterials[i];
                    if (sourceMaterial == null)
                    {
                        continue;
                    }
                    string tag = sourceMaterial.GetTag("RenderType", true);
                    if (tag == "Transparent" || tag == "TransparentCutout")
                    {
                        Material replacementMaterial = new Material(writeDepth ? transparentZShader : transparentShader);
                        if (sourceMaterial.HasProperty("_MainTex"))
                        {
                            replacementMaterial.SetTexture("_MainTex", sourceMaterial.mainTexture);
                            replacementMaterial.SetTextureOffset("_MainTex", sourceMaterial.mainTextureOffset);
                            replacementMaterial.SetTextureScale("_MainTex", sourceMaterial.mainTextureScale);
                        }

                        replacementMaterial.SetFloat("_Cutoff", sourceMaterial.HasProperty("_Cutoff") ? sourceMaterial.GetFloat("_Cutoff") : TransparentCutoff);

                        _replacementMaterials[i] = replacementMaterial;
                        _transparentMaterialIndexes.Add(i);
                    }
                    else
                    {
                        _replacementMaterials[i] = sharedOpaqueMaterial;
                    }
                }
            }

            public void SetState(bool highlightingState)
            {
                RendererCached.sharedMaterials = highlightingState ? _replacementMaterials : _sourceMaterials;
            }

            public void SetColorForTransparent(Color color)
            {
                for (int i = 0; i < _transparentMaterialIndexes.Count; i++)
                {
                    _replacementMaterials[_transparentMaterialIndexes[i]].SetColor("_Outline", color);
                }
            }
        }

        private void OnEnable()
        {
            StartCoroutine(EndOfFrame());
            HighlightingEffect.HighlightingEvent += UpdateHighlighting;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            HighlightingEffect.HighlightingEvent -= UpdateHighlighting;

            if (_highlightableRenderers != null)
            {
                _highlightableRenderers.Clear();
            }

            //���ø�������
            _layersCache = null;
            _materialsIsDirty = true;
            _currentHighlightingState = false;
            _currentHighlightingColor = Color.clear;
            _transitionActive = false;
            _transitionValue = 0f;
            _isOnce = false;
            _isFlashing = false;
            _isConstantly = false;
            _isOccluder = false;
            _isZWrite = false;
            
            if (_opaqueMaterial)
            {
                DestroyImmediate(_opaqueMaterial);
            }

            if (_opaqueZMaterial)
            {
                DestroyImmediate(_opaqueZMaterial);
            }
        }
        #endregion

        #region public Methods
        /// <summary>
        /// ���³�ʼ������
        /// </summary>
        public void ReinitMaterials()
        {
            _materialsIsDirty = true;
        }

        /// <summary>
        /// ����ֻ����һ֡�Ĳ���
        /// </summary>
        /// <param name="color">��ɫ</param>
        public void SetOnceParams(Color color)
        {
            _onceColor = color;
        }

        /// <summary>
        /// ��������һ֡
        /// </summary>
        public void OpenOnce()
        {
            _isOnce = true;
        }

        /// <summary>
        /// ��������һ֡
        /// </summary>
        /// <param name="color">��ɫ</param>
        public void OpenOnce(Color color)
        {
            _onceColor = color;
            _isOnce = true;
        }

        /// <summary>
        /// �����������
        /// </summary>
        /// <param name="color1">���⿪ʼ��ɫ</param>
        /// <param name="color2">���������ɫ</param>
        /// <param name="freq">����Ƶ��</param>
        public void SetFlashingParams(Color color1, Color color2, float freq)
        {
            _flashingColorMin = color1;
            _flashingColorMax = color2;
            _flashingFrequency = freq;
        }

        /// <summary>
        /// ��������
        /// </summary>
        public void OpenFlashing()
        {
            _isFlashing = true;
        }

        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="color1">���⿪ʼ��ɫ</param>
        /// <param name="color2">���������ɫ</param>
        public void OpenFlashing(Color color1, Color color2)
        {
            _flashingColorMin = color1;
            _flashingColorMax = color2;
            _isFlashing = true;
        }

        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="color1">���⿪ʼ��ɫ</param>
        /// <param name="color2">���������ɫ</param>
        /// <param name="freq">����Ƶ��</param>
        public void OpenFlashing(Color color1, Color color2, float freq)
        {
            _flashingColorMin = color1;
            _flashingColorMax = color2;
            _flashingFrequency = freq;
            _isFlashing = true;
        }

        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="freq">����Ƶ��</param>
        public void OpenFlashing(float freq)
        {
            _flashingFrequency = freq;
            _isFlashing = true;
        }

        /// <summary>
        /// �ر�����
        /// </summary>
        public void CloseFlashing()
        {
            _isFlashing = false;
        }

        /// <summary>
        /// ����ģʽ�л�
        /// </summary>
        public void FlashingSwitch()
        {
            _isFlashing = !_isFlashing;
        }

        /// <summary>
        /// ���ó�����������
        /// </summary>
        /// <param name="color">��ɫ</param>
        public void SetConstantParams(Color color)
        {
            _constantColor = color;
        }

        /// <summary>
        /// ������������
        /// </summary>
        public void OpenConstant()
        {
            _isConstantly = true;
            _transitionActive = true;
        }

        /// <summary>
        /// ������������
        /// </summary>
        /// <param name="color">��ɫ</param>
        public void OpenConstant(Color color)
        {
            _constantColor = color;
            _isConstantly = true;
            _transitionActive = true;
        }

        /// <summary>
        /// �رճ�������
        /// </summary>
        public void CloseConstant()
        {
            _isConstantly = false;
            _transitionActive = true;
        }

        /// <summary>
        /// ��������ģʽ�л�
        /// </summary>
        public void ConstantSwitch()
        {
            _isConstantly = !_isConstantly;
            _transitionActive = true;
        }

        /// <summary>
        /// ����������������
        /// </summary>
        public void OpenConstantImmediate()
        {
            _isConstantly = true;
            _transitionValue = 1f;
            _transitionActive = false;
        }

        /// <summary>
        /// ����������������
        /// </summary>
        /// <param name="color">��ɫ</param>
        public void OpenConstantImmediate(Color color)
        {
            _constantColor = color;
            _isConstantly = true;
            _transitionValue = 1f;
            _transitionActive = false;
        }

        /// <summary>
        /// �����رճ�������
        /// </summary>
        public void CloseConstantImmediate()
        {
            _isConstantly = false;
            _transitionValue = 0f;
            _transitionActive = false;
        }

        /// <summary>
        /// ��������ģʽ�����л�
        /// </summary>
        public void ConstantSwitchImmediate()
        {
            _isConstantly = !_isConstantly;
            _transitionValue = _isConstantly ? 1f : 0f;
            _transitionActive = false;
        }

        /// <summary>
        /// �����ڹ��
        /// </summary>
        public void OpenOccluder()
        {
            _isOccluder = true;
        }

        /// <summary>
        /// �ر��ڹ��
        /// </summary>
        public void CloseOccluder()
        {
            _isOccluder = false;
        }

        /// <summary>
        /// �ڹ��ģʽ�л�
        /// </summary>
        public void OccluderSwitch()
        {
            _isOccluder = !_isOccluder;
        }

        /// <summary>
        /// �ر����и���ģʽ
        /// </summary>
        public void CloseAll()
        {
            _isOnce = false;
            _isFlashing = false;
            _isConstantly = false;
            _isOccluder = false;
            _transitionValue = 0f;
            _transitionActive = false;
        }

        /// <summary>
        /// ����
        /// </summary>
        public void Die()
        {
            Destroy(this);
        }
        #endregion

        #region Private Methods
        private void InitMaterials(bool writeDepth)
        {
            _currentHighlightingState = false;

            _isZWrite = writeDepth;

            _highlightableRenderers = new List<HighlightingRendererCache>();

            MeshRenderer[] mr = GetComponentsInChildren<MeshRenderer>();
            CacheRenderers(mr);

            SkinnedMeshRenderer[] smr = GetComponentsInChildren<SkinnedMeshRenderer>();
            CacheRenderers(smr);

            _currentHighlightingState = false;
            _materialsIsDirty = false;
            _currentHighlightingColor = Color.clear;
        }

        private void CacheRenderers(Renderer[] renderers)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;

                if (materials != null)
                {
                    _highlightableRenderers.Add(new HighlightingRendererCache(renderers[i], materials, _highlightingMaterial, _isZWrite));
                }
            }
        }

        private void SetColor(Color color)
        {
            if (_currentHighlightingColor == color)
            {
                return;
            }

            if (_isZWrite)
            {
                opaqueZMaterial.SetColor("_Outline", color);
            }
            else
            {
                opaqueMaterial.SetColor("_Outline", color);
            }

            for (int i = 0; i < _highlightableRenderers.Count; i++)
            {
                _highlightableRenderers[i].SetColorForTransparent(color);
            }

            _currentHighlightingColor = color;
        }

        private void UpdateColors()
        {
            if (_currentHighlightingState == false)
            {
                return;
            }

            if (_isOccluder)
            {
                SetColor(_occluderColor);
                return;
            }

            if (_isOnce)
            {
                SetColor(_onceColor);
                return;
            }

            if (_isFlashing)
            {
                Color color = Color.Lerp(_flashingColorMin, _flashingColorMax, 0.5f * Mathf.Sin(Time.realtimeSinceStartup * _flashingFrequency * DoublePI) + 0.5f);
                SetColor(color);
                return;
            }

            if (_transitionActive)
            {
                Color color = new Color(_constantColor.r, _constantColor.g, _constantColor.b, _constantColor.a * _transitionValue);
                SetColor(color);
                return;
            }
            else if (_isConstantly)
            {
                SetColor(_constantColor);
                return;
            }
        }

        private void PerformTransition()
        {
            if (_transitionActive == false)
            {
                return;
            }

            float targetValue = _isConstantly ? 1f : 0f;

            if (_transitionValue == targetValue)
            {
                _transitionActive = false;
                return;
            }

            if (!Time.timeScale.Approximately(0f))
            {
                float unscaledDeltaTime = Time.deltaTime / Time.timeScale;
                _transitionValue += (_isConstantly ? ConstantOnSpeed : -ConstantOffSpeed) * unscaledDeltaTime;
                _transitionValue = Mathf.Clamp01(_transitionValue);
            }
            else
            {
                return;
            }
        }

        private void UpdateHighlighting(bool enable, bool writeDepth)
        {
            if (enable)
            {
                if (_isZWrite != writeDepth)
                {
                    _materialsIsDirty = true;
                }

                if (_materialsIsDirty)
                {
                    InitMaterials(writeDepth);
                }

                _currentHighlightingState = _isOnce || _isFlashing || _isConstantly || _transitionActive || _isOccluder;

                if (_currentHighlightingState)
                {
                    UpdateColors();

                    PerformTransition();

                    if (_highlightableRenderers != null)
                    {
                        _layersCache = new int[_highlightableRenderers.Count];
                        for (int i = 0; i < _highlightableRenderers.Count; i++)
                        {
                            GameObject go = _highlightableRenderers[i].GameObjectCached;
                            _layersCache[i] = go.layer;
                            go.layer = HighlightingLayer;
                            _highlightableRenderers[i].SetState(true);
                        }
                    }
                }
            }
            else
            {
                if (_currentHighlightingState && _highlightableRenderers != null)
                {
                    for (int i = 0; i < _highlightableRenderers.Count; i++)
                    {
                        _highlightableRenderers[i].GameObjectCached.layer = _layersCache[i];
                        _highlightableRenderers[i].SetState(false);
                    }
                }
            }
        }

        private IEnumerator EndOfFrame()
        {
            while (enabled)
            {
                yield return YieldInstructioner.GetWaitForEndOfFrame();
                _isOnce = false;
            }
        }
        #endregion
    }
}