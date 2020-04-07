using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace NoteSliceVisualizer
{
    public class SliceController : MonoBehaviour
    {
        float _uiScale;
        RectTransform _blockTransform;
        RectTransform _blockMaskTransform;
        RectTransform _noteMaskTransform;
        RectTransform _noteMaskInverseTransform;
        RectTransform _sliceTransform;
        RectTransform _cutOffsetTransform;
        RectTransform _noteArrowTransform;
        CanvasGroup _canvasGroup;
        RawImage _backgroundImage;
        RawImage _noteArrowImage;
        RawImage _cutSliceImage;
        RawImage _cutOffsetImage;
        Color _backgroundColor;
        float _timeSinceSliced;
        bool _isAlive = true;
        Texture _cutLineTexture = ConfigHelper.Config.CutLineUseTriangleTexture ? AssetBundleHelper.TriangleTexture : null;
        float _noteArrowAlpha = ConfigHelper.Config.NoteArrowAlpha;
        Color _cutLineColor = ConfigHelper.Config.CutLineColor;
        Color _cutOffsetColor = ConfigHelper.Config.CutOffsetColor;
        float _cutLineWidth = ConfigHelper.Config.CutLineWidth;
        float _cutLineLengthScale = ConfigHelper.Config.CutLineLengthScale;
        float _cutLineSensitivity = ConfigHelper.Config.CutLineSensitivity;

        bool _shouldRotateUIWithNote = ConfigHelper.Config.RotateUIWithNote;
        bool _shouldUpdateColor = !ConfigHelper.Config.TwoNoteMode;
        float _maxAlpha = ConfigHelper.Config.Alpha;
        float _popDuration = ConfigHelper.Config.PopDuration;
        float _delayDuration = ConfigHelper.Config.DelayDuration;
        float _fadeDuration = ConfigHelper.Config.FadeDuration;

        public void Init(GameObject noteUI)
        {
            _blockTransform = noteUI.GetComponentsInChildren<RectTransform>().First(o => o.name == "Block");
            _blockMaskTransform = noteUI.GetComponentsInChildren<RectTransform>().First(o => o.name == "BlockMask");
            _noteArrowTransform = noteUI.GetComponentsInChildren<RectTransform>().First(o => o.name == "NoteArrowImage");
            _noteMaskTransform = noteUI.GetComponentsInChildren<RectTransform>().First(o => o.name == "NoteMask");
            _noteMaskInverseTransform = noteUI.GetComponentsInChildren<RectTransform>().First(o => o.name == "NoteMaskInverse");
            _sliceTransform = noteUI.GetComponentsInChildren<RectTransform>().First(o => o.name == "NoteSlice");
            _cutOffsetTransform = noteUI.GetComponentsInChildren<RectTransform>().First(o => o.name == "MissedAreaImage");
            _canvasGroup = noteUI.GetComponent<CanvasGroup>();

            _backgroundImage = _blockTransform.GetComponent<RawImage>();
            _noteArrowImage = _noteArrowTransform.GetComponent<RawImage>();
            _cutSliceImage = _sliceTransform.GetComponent<RawImage>();
            _cutOffsetImage = _cutOffsetTransform.GetComponent<RawImage>();

            _uiScale = _blockTransform.rect.width * 1.1f;
            _cutSliceImage.texture = _cutLineTexture;
            _cutSliceImage.color = _cutLineColor;
            _cutOffsetImage.color = _cutOffsetColor;

            _noteMaskTransform.sizeDelta *= _cutLineLengthScale;
            float cutLineHeight = _sliceTransform.sizeDelta.x * _cutLineLengthScale;
            _sliceTransform.sizeDelta = new Vector2(cutLineHeight, _cutLineWidth);

            if (_popDuration <= 0) _popDuration = 0.001f;
            if (_delayDuration <= 0) _delayDuration = 0.001f;
            if (_fadeDuration <= 0) _fadeDuration = 0.001f;
            _timeSinceSliced = _fadeDuration + _delayDuration;
        }

        public void UpdateBlockColor(Color color)
        {
            _backgroundColor = color;
            _backgroundImage.color = color;
        }

        public void UpdateSlice(Vector3 cutPoint, Vector3 cutPlaneNormal, NoteCutDirection noteDirectionType)
        {
            cutPoint = new Vector3(cutPoint.x, cutPoint.y);
            Vector3 uiCutPoint = cutPoint * _uiScale * _cutLineSensitivity;

            Vector3 cutDirection = new Vector3(-cutPlaneNormal.y, cutPlaneNormal.x).normalized;
            float cutRotation = Mathf.Atan2(cutDirection.y, cutDirection.x) * Mathf.Rad2Deg;

            float noteArrowAlpha = noteDirectionType >= NoteCutDirection.Any ? 0f : _noteArrowAlpha;
            _noteArrowImage.color = new Color(1f, 1f, 1f, noteArrowAlpha);
            float noteRotation = RotationFromNoteCutDirection(noteDirectionType);
            SetBackgroundRotation(noteRotation);

            _sliceTransform.localPosition = uiCutPoint;
            _sliceTransform.localRotation = Quaternion.Euler(0f, 0f, cutRotation);

            bool isCenterIsLeft = IsCenterLeftOfCut(cutPlaneNormal, cutPoint);
            float missedAreaRotation = cutRotation - noteRotation + (isCenterIsLeft ? 180f : 0f);

            float cutPointDistance = cutPoint.magnitude;
            _cutOffsetTransform.sizeDelta = new Vector2(_cutOffsetTransform.rect.width, cutPointDistance * _uiScale * _cutLineSensitivity);
            _cutOffsetTransform.localRotation = Quaternion.Euler(0f, 0f, missedAreaRotation);

            _timeSinceSliced = 0f;
            _isAlive = true;
        }

        public void Update()
        {
            if (_shouldUpdateColor && _canvasGroup != null && _isAlive)
            {
                float popT = _timeSinceSliced / _popDuration;
                float fadeT = (_timeSinceSliced - _delayDuration) / _fadeDuration;

                float pop = Mathf.Lerp(10f, 1f, popT);
                float a = Mathf.Lerp(_maxAlpha, 0f, fadeT);

                _backgroundImage.color = _backgroundColor * pop;
                _canvasGroup.alpha = a;

                if (fadeT >= 1.0f)
                {
                    _isAlive = false;
                }
                else
                {
                    _timeSinceSliced += Time.deltaTime;
                }
            }
        }

        private bool IsCenterLeftOfCut(Vector3 cutPlaneNormal, Vector3 cutPoint)
        {
            Vector3 lineNormal = new Vector3(cutPlaneNormal.x, cutPlaneNormal.y);
            return Vector3.Dot(lineNormal, -cutPoint) > 0f;
        }

        private float CutAccuracy(float cutPointDistance)
        {
            return Mathf.Clamp01(cutPointDistance / 0.3f);
        }

        private float RotationFromNoteCutDirection(NoteCutDirection cutDirection)
        {
            if (!_shouldRotateUIWithNote)
            {
                return 0f;
            }
            switch (cutDirection)
            {
                case NoteCutDirection.Down: return 0f;
                case NoteCutDirection.DownRight: return 45f;
                case NoteCutDirection.Right: return 90f;
                case NoteCutDirection.UpRight: return 135f;
                case NoteCutDirection.Up: return 180f;
                case NoteCutDirection.UpLeft: return 225f;
                case NoteCutDirection.Left: return 270f;
                case NoteCutDirection.DownLeft: return 315f;
                default: return 0f;
            }
        }

        private void SetBackgroundRotation(float rotation)
        {
            Quaternion rotationQuaternion = Quaternion.Euler(0f, 0f, rotation);
            Quaternion inverseRotationQuaternion = Quaternion.Euler(0f, 0f, -rotation);

            _blockTransform.localRotation = rotationQuaternion;
            _blockMaskTransform.localRotation = rotationQuaternion;
            _noteMaskTransform.localRotation = rotationQuaternion;
            _noteMaskInverseTransform.localRotation = inverseRotationQuaternion;
        }

    }
}
