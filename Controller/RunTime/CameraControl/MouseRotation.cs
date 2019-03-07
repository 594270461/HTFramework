using UnityEngine;

namespace HT.Framework
{
    /// <summary>
    /// �����ע��Ŀ����ת����
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class MouseRotation : MonoBehaviour
    {
        //ע��Ŀ��
        public CameraTarget Target;
        //ע�ӵ�x��y��ƫ�ƣ�����Ϊ0����ע�ӵ����ע��Ŀ���transform.position������Ŀ���ǽ�ɫʱ��ע�ӵ���ܻ�����ƫ��
        public float OffsetY = 0f;
        public float OffsetX = 0f;
        //x����ת�ٶȣ�y����ת�ٶȣ����������ٶ�
        public float XSpeed = 150, YSpeed = 150, MSpeed = 30;
        //y���ӽ����ֵ��y���ӽ����ֵ��Ŀ���ǽ�ɫ�Ļ����Ƽ���С10�����85���������Ͳ��ᵽ��ɫ�ŵף���߲��ᵽ��ɫͷ�����Ϸ���
        //�����ӽ����ֵΪ10����ô�������y����ת��Сֻ�ܵ�10������С��0��������0�����Ŀ���ǽ�ɫ�Ļ���Ҳ���������ֻ�ܽӽ�����Ȼ��Ͳ������¼����ƶ���
        //�����ӽ����ֵΪ85����ô�������y����ת���ֻ�ܵ�85���������85�������࣬���Ŀ���ǽ�ɫ�Ļ���Ҳ���������ֻ�ܽӽ���ɫͷ�������ܵ���90����ȫ��ֱ���¿����ɫ
        public float YMinAngleLimit = -85, YMaxAngleLimit = 85;
        //�������ע��Ŀ�����
        public float Distance = 2.0f;
        //�������ע��Ŀ����С����
        public float MinDistance = 0;
        //�������ע��Ŀ��������
        public float MaxDistance = 4;
        //�������Χ��Ŀ����ת�����Ƿ��ǲ�ֵ����
        public bool NeedDamping = true;
        //��ʼ�������x��ת��y��ת���������Χ��Ŀ����ת��������ֵ�᲻ͣ�ı䣬���ó�ʼֵ����ʹ�������ʼ�����ɫ��ָ�����������ʼ�����ɫ����
        public float X = 90.0f;
        public float Y = 30.0f;
        //������Ƿ��޶�λ��
        public bool NeedLimit = false;
        //x��λ�����ֵ��x��λ�����ֵ
        public float XMinLimit = -5, XMaxLimit = 5;
        //y��λ�����ֵ��y��λ�����ֵ
        public float YMinLimit = 0.1f, YMaxLimit = 5;
        //z��λ�����ֵ��z��λ�����ֵ
        public float ZMinLimit = -5, ZMaxLimit = 5;
        //��UGUIĿ�����Ƿ���Կ���
        public bool IsCanOnUGUI = false;
        
        //ע�ӵ㣨ע��Ŀ���׼ȷλ�ã�����ƫ�ƺ��λ�ã�
        private Vector3 _targetPoint;
        //��ֵ��
        private float _damping = 5.0f;
        //ϵ��
        private float _factor = 0.02f;

        //Ŀ��λ��
        private Quaternion _rotation;
        private Vector3 _position;
        private Vector3 _disVector;
        //���յ�λ��
        private Vector3 _finalPosition;

        /// <summary>
        /// �Ƿ���Կ���
        /// </summary>
        public bool CanControl { get; set; } = true;

        /// <summary>
        /// ������ת�޶���Сֵ
        /// </summary>
        public void SetMinLimit(Vector3 value)
        {
            XMinLimit = value.x;
            YMinLimit = value.y;
            ZMinLimit = value.z;
        }

        /// <summary>
        /// ������ת�޶����ֵ
        /// </summary>
        public void SetMaxLimit(Vector3 value)
        {
            XMaxLimit = value.x;
            YMaxLimit = value.y;
            ZMaxLimit = value.z;
        }

        /// <summary>
        /// ����ע���ӽ�
        /// </summary>
        public void SetAngle(Vector3 angle, bool damping)
        {
            X = angle.x;
            Y = angle.y;
            Distance = angle.z;

            if (!damping)
            {
                CalculateAngle();
                SwitchAngle(damping);
            }
        }

        /// <summary>
        /// ����ע���ӽ�
        /// </summary>
        public void SetAngle(Vector2 angle, float distance, bool damping)
        {
            X = angle.x;
            Y = angle.y;
            Distance = distance;

            if (!damping)
            {
                CalculateAngle();
                SwitchAngle(damping);
            }
        }
        
        /// <summary>
        /// ˢ��
        /// </summary>
        public void Refresh()
        {
            //����
            Control();
            //Ӧ��
            ApplyRotation();
        }

        private void Control()
        {
            if (!CanControl)
                return;

            if (!IsCanOnUGUI && GlobalTools.IsPointerOverUGUI())
                return;

            if (Input.GetMouseButton(1))
            {
                //��¼�������ƶ���
                X += Input.GetAxis("Mouse X") * XSpeed * _factor;
                //��¼��������ƶ���
                Y -= Input.GetAxis("Mouse Y") * YSpeed * _factor;
            }
            if (Input.GetAxis("Mouse ScrollWheel") != 0)
            {
                //�����������ӽǾ���
                Distance -= Input.GetAxis("Mouse ScrollWheel") * MSpeed * Time.deltaTime;

                if (Distance <= MinDistance)
                {
                    Target.transform.Translate(transform.forward * Input.GetAxis("Mouse ScrollWheel"));
                }
            }
        }

        private void ApplyRotation()
        {
            //���¼����ӽ�
            CalculateAngle();

            //�л��ӽ�
            SwitchAngle(NeedDamping);

            //�����һֱ����ע��Ŀ���
            transform.LookAt(_targetPoint);
        }

        private void CalculateAngle()
        {
            //�������ƶ����������ӽ����ֵ�����ֵ֮��
            Y = ClampYAngle(Y, YMinAngleLimit, YMaxAngleLimit);
            //���ӽǾ�����������Сֵ�����ֵ֮��
            Distance = Mathf.Clamp(Distance, MinDistance, MaxDistance);

            //���»�ȡ�����ע�ӵ�
            _targetPoint.Set(Target.transform.position.x + OffsetX, Target.transform.position.y + OffsetY, Target.transform.position.z);

            //�������������ת�Ƕ�
            _rotation = Quaternion.Euler(Y, X, 0.0f);
            //�������ע�ӵ�ľ�������
            _disVector.Set(0.0f, 0.0f, -Distance);
            //�������������ת�Ƕ�*�������ע�ӵ�ľ��룬�ó��������ע�ӵ�����λ�ã�����ע�ӵ��λ�ü������λ�ñ�����������λ��
            _position = Target.transform.position + _rotation * _disVector;
        }

        private void SwitchAngle(bool damping)
        {
            //�������ֵ�任���µ�λ��
            if (damping)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, _rotation, Time.deltaTime * _damping);
                transform.position = Vector3.Lerp(transform.position, _position, Time.deltaTime * _damping);
            }
            //�����ֱ�ӱ任���µ�λ��
            else
            {
                transform.rotation = _rotation;
                transform.position = _position;
            }

            //�����λ������
            if (NeedLimit)
            {
                if (transform.position.x < XMinLimit)
                {
                    _finalPosition.Set(XMinLimit, transform.position.y, transform.position.z);
                    transform.position = _finalPosition;
                }
                else if (transform.position.x > XMaxLimit)
                {
                    _finalPosition.Set(XMaxLimit, transform.position.y, transform.position.z);
                    transform.position = _finalPosition;
                }

                if (transform.position.y < YMinLimit)
                {
                    _finalPosition.Set(transform.position.x, YMinLimit, transform.position.z);
                    transform.position = _finalPosition;
                }
                else if (transform.position.y > YMaxLimit)
                {
                    _finalPosition.Set(transform.position.x, YMaxLimit, transform.position.z);
                    transform.position = _finalPosition;
                }

                if (transform.position.z < ZMinLimit)
                {
                    _finalPosition.Set(transform.position.x, transform.position.y, ZMinLimit);
                    transform.position = _finalPosition;
                }
                else if (transform.position.z > ZMaxLimit)
                {
                    _finalPosition.Set(transform.position.x, transform.position.y, ZMaxLimit);
                    transform.position = _finalPosition;
                }
            }
        }

        private float ClampYAngle(float angle, float min, float max)
        {
            if (angle < -360)
                angle += 360;
            if (angle > 360)
                angle -= 360;

            return Mathf.Clamp(angle, min, max);
        }
    }
}