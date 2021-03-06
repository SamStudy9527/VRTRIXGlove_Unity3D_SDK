﻿//============= Copyright (c) VRTRIX INC, All rights reserved. ================
//
// Purpose: Example CSharp script to read data stream using APIs provided in
//          wrapper class. A simple GUI inluding VRTRIX Digital Glove status and 
//          sensor data is provided by this script.
//
//=============================================================================
using UnityEngine;
using System;
using System.Threading;
using Valve.VR;

namespace VRTRIX
{
    [RequireComponent(typeof(VRTRIXBoneMapping))]

    //!  VRTRIX Data Glove data streaming class. 
    /*!
        A basic data streaming class for demonstration.
    */
    public class VRTRIXGloveDataStreaming : MonoBehaviour
    {
        [Header("VR Settings")]
        //! VR environment enable flag, set to true if run the demo in VR headset
        public bool IsVREnabled = false;

        [DrawIf("IsVREnabled", false)]
        //! If VR is NOT enabled, wrist joint need an object to align, which can be the camera, or parent joint of wrist(if a full body model is used), or can just be any other game objects.
        public GameObject LH_ObjectToAlign;

        [DrawIf("IsVREnabled", false)]
        //! If VR is NOT enabled, wrist joint need an object to align, which can be the camera, or parent joint of wrist(if a full body model is used), or can just be any other game objects.
        public GameObject RH_ObjectToAlign;

        [DrawIf("IsVREnabled", true)]
        //! If VR is enabled, HTC tracker is the default wrist tracking hardware, which is fixed to side part of data glove, this offset represents the offset between tracker origin to right wrist joint origin.
        public Vector3 RHTrackerOffset = new Vector3(0.01f, 0, -0.035f);

        [DrawIf("IsVREnabled", true)]
        //! If VR is enabled, HTC tracker is the default wrist tracking hardware, which is fixed to side part of data glove, this offset represents the offset between tracker origin to left wrist joint origin.
        public Vector3 LHTrackerOffset = new Vector3(-0.01f, 0, -0.035f);

        [Header("Glove Settings")]
        //! Hardware version of VRTRIX data gloves, currently DK1, DK2 and PRO are supported.
        public GLOVEVERSION version;

        //! Mutiple gloves enable flag, set to true if run multiple gloves on the same PC.
        public bool IsEnableMultipleGloves;

        //! If mutiple gloves mode is enbaled, specify different index for different pair of gloves. Otherwise, just select None.
        public GloveIndex Index;

        [Header("Model Mapping Settings")]
        //! Model mapping parameters for left hand, only used when finger joint axis definition is different from wrist joint, otherwise, just set to 0,0,0.
        public Vector3 ql_modeloffset;

        //! Model mapping parameters for right hand, only used when finger joint axis definition is different from wrist joint, otherwise, just set to 0,0,0.
        public Vector3 qr_modeloffset;

        //! Model mapping parameters for left hand, only used when wrist joint axis definition is different from hardware wrist joint, otherwise, just set to identity matrix {(1,0,0),(0,1,0),(0,0,1)}. Please read the sdk tutorial documentation to learn how to set this parameter properly.
        public Vector3[] ql_axisoffset = new Vector3[3];

        //! Model mapping parameters for right hand, only used when wrist joint axis definition is different from hardware wrist joint, otherwise, just set to identity matrix {(1,0,0),(0,1,0),(0,0,1)}. Please read the sdk tutorial documentation to learn how to set this parameter properly.
        public Vector3[] qr_axisoffset = new Vector3[3];

        [Header("Thumb Parameters")]
        //! Model mapping parameters for left thumb joint, used to tune thumb offset between the model and hardware sensor placement. Please read the sdk tutorial documentation to learn how to set this parameter properly.
        public Vector3[] thumb_offset_L = new Vector3[3];
        
        //! Model mapping parameters for right thumb joint, used to tune thumb offset between the model and hardware sensor placement. Please read the sdk tutorial documentation to learn how to set this parameter properly.
        public Vector3[] thumb_offset_R = new Vector3[3];

        //! Model mapping parameters for thumb proximal joint, used to tune thumb slerp algorithm parameter. Please read the sdk tutorial documentation to learn how to set this parameter properly.
        public double thumb_proximal_slerp;

        //! Model mapping parameters for thumb middle joint, used to tune thumb slerp algorithm parameter. Please read the sdk tutorial documentation to learn how to set this parameter properly.
        public double thumb_middle_slerp;

        [Header("Finger Parameters")]
        //! Finger spacing when advanced mode is NOT enabled. Please read the sdk tutorial documentation to learn how to set this parameter properly.
        public double finger_spacing;

        //! Finger spacing when four fingers are fully bended. Please read the sdk tutorial documentation to learn how to set this parameter properly.
        public double final_finger_spacing;


        public VRTRIXDataWrapper LH, RH;
        private GameObject LH_tracker, RH_tracker;
        private VRTRIXGloveGestureRecognition GloveGesture;
        private Thread LH_receivedData, RH_receivedData;
        private Quaternion qloffset, qroffset;
        private bool qloffset_cal, qroffset_cal;
        private VRTRIXGloveGesture LH_Gesture, RH_Gesture = VRTRIXGloveGesture.BUTTONNONE;
        private bool LH_Mode, RH_Mode;
        private Transform[] fingerTransformArray;
        private Matrix4x4 ml_axisoffset, mr_axisoffset;
        private  bool AdvancedMode = false;

        void Start()
        {
            if (!IsEnableMultipleGloves)
            {
                Index = GloveIndex.MaxDeviceCount;
            }
            LH = new VRTRIXDataWrapper(AdvancedMode, (int)Index, version);
            RH = new VRTRIXDataWrapper(AdvancedMode, (int)Index, version);
            GloveGesture = new VRTRIXGloveGestureRecognition();
            fingerTransformArray = FindFingerTransform();
            for(int i = 0; i < 3; i++)
            {
                ml_axisoffset.SetRow(i, ql_axisoffset[i]);
                mr_axisoffset.SetRow(i, qr_axisoffset[i]);
            }
            ml_axisoffset.SetRow(3, Vector3.forward);
            mr_axisoffset.SetRow(3, Vector3.forward);

            if (IsVREnabled)
            {
                try
                {
                    RH_tracker = CheckDeviceModelName(HANDTYPE.RIGHT_HAND);
                    LH_tracker = CheckDeviceModelName(HANDTYPE.LEFT_HAND);
                }
                catch (Exception e)
                {
                    print("Exception caught: " + e);
                }
            }
        }

        void Update()
        {
            if (RH_Mode && RH.GetReceivedStatus() == VRTRIXGloveStatus.NORMAL)
            {
                if (RH.GetReceivedRotation(VRTRIXBones.R_Hand) != Quaternion.identity && !qroffset_cal)
                {
                    qroffset = CalculateStaticOffset(RH, HANDTYPE.RIGHT_HAND);
                    qroffset_cal = true;
                }

                if (IsVREnabled && RH_tracker != null)
                {
                    SetPosition(VRTRIXBones.R_Arm, RH_tracker.transform.position, RH_tracker.transform.rotation, RHTrackerOffset);
                }
                //以下是设置右手每个骨骼节点全局旋转(global rotation)；
                for(int i = 0; i < (int)VRTRIXBones.L_Hand; ++i)
                {
                    SetRotation((VRTRIXBones)i, RH.GetReceivedRotation((VRTRIXBones)i), HANDTYPE.RIGHT_HAND);
                }

                RH.SetThumbOffset(thumb_offset_R[0], VRTRIXBones.R_Thumb_1);
                RH.SetThumbOffset(thumb_offset_R[1], VRTRIXBones.R_Thumb_2);
                RH.SetThumbOffset(thumb_offset_R[2], VRTRIXBones.R_Thumb_3);
                RH.SetThumbSlerpRate(thumb_proximal_slerp, thumb_middle_slerp);
                RH.SetFinalFingerSpacing(final_finger_spacing);
                RH.SetFingerSpacing(finger_spacing);
                RH_Gesture = GloveGesture.GestureDetection(RH, HANDTYPE.RIGHT_HAND);
            }



            if (LH_Mode && LH.GetReceivedStatus() == VRTRIXGloveStatus.NORMAL)
            {
                if (LH.GetReceivedRotation(VRTRIXBones.L_Hand) != Quaternion.identity && !qloffset_cal)
                {
                    qloffset = CalculateStaticOffset(LH, HANDTYPE.LEFT_HAND);
                    qloffset_cal = true;
                }

                if (IsVREnabled && LH_tracker != null)
                {
                    SetPosition(VRTRIXBones.L_Arm, LH_tracker.transform.position, LH_tracker.transform.rotation, LHTrackerOffset);
                }

                //以下是设置左手每个骨骼节点全局旋转(global rotation)；
                for(int i = (int)VRTRIXBones.L_Hand; i < (int)VRTRIXBones.R_Arm; ++i)
                {
                    SetRotation((VRTRIXBones)i, LH.GetReceivedRotation((VRTRIXBones)i), HANDTYPE.LEFT_HAND);
                }

                LH.SetThumbOffset(thumb_offset_L[0], VRTRIXBones.L_Thumb_1);
                LH.SetThumbOffset(thumb_offset_L[1], VRTRIXBones.L_Thumb_2);
                LH.SetThumbOffset(thumb_offset_L[2], VRTRIXBones.L_Thumb_3);
                LH.SetThumbSlerpRate(thumb_proximal_slerp, thumb_middle_slerp);
                LH.SetFinalFingerSpacing(final_finger_spacing);
                LH.SetFingerSpacing(finger_spacing);
                LH_Gesture = GloveGesture.GestureDetection(LH, HANDTYPE.LEFT_HAND);
            }
        }

        void OnGUI()
        {
            if (IsEnableMultipleGloves) return;
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = (int)(25 * (Screen.height / 1440.0));

            if (GetReceivedStatus(HANDTYPE.LEFT_HAND) == VRTRIXGloveStatus.CLOSED && GetReceivedStatus(HANDTYPE.RIGHT_HAND) == VRTRIXGloveStatus.CLOSED)
            {
                if (GUI.Button(new Rect(0, 0, Screen.width / 10, Screen.height / 10), "Connect", buttonStyle))
                {
                    OnConnectGlove();
                }
            }
            else
            {
                if (GUI.Button(new Rect(0, 0, Screen.width / 10, Screen.height / 10), "Disconnect", buttonStyle))
                {
                    OnDisconnectGlove();
                }
            }

            if (GUI.Button(new Rect(0, Screen.height / 10, Screen.width / 10, Screen.height / 10), "Reset View", buttonStyle))
            {
                OnAlignWrist();
            }


            if (!IsVREnabled)
            {
                if (GUI.Button(new Rect(0, Screen.height * (2.0f / 10.0f), Screen.width / 10, Screen.height / 10), "Align Fingers", buttonStyle))
                {
                    OnAlignFingers();
                }

                if (GUI.Button(new Rect(0, Screen.height * (3.0f / 10.0f), Screen.width / 10, Screen.height / 10), "Save Calibration", buttonStyle))
                {
                    OnHardwareCalibrate();
                }
    
                if (GUI.Button(new Rect(0, Screen.height * (4.0f / 10.0f), Screen.width / 10, Screen.height / 10), "Trigger Haptic", buttonStyle))
                {
                    OnVibrate();
                }
    
                if (GUI.Button(new Rect(0, Screen.height * (5.0f / 10.0f), Screen.width / 10, Screen.height / 10), "Channel Hopping", buttonStyle))
                {
                    OnChannelHopping();
                }
            }
        }

        //数据手套初始化，硬件连接
        //! Connect data glove and initialization.
        public void OnConnectGlove()
        {
            if (IsVREnabled && LH_tracker == null && RH_tracker == null) return;
            try
            {
                LH_Mode = LH.Init(HANDTYPE.LEFT_HAND);
                if (LH_Mode)
                {
                    print("Left hand glove connected!");
                    LH.SetThumbOffset(thumb_offset_L[0], VRTRIXBones.L_Thumb_1);
                    LH.SetThumbOffset(thumb_offset_L[1], VRTRIXBones.L_Thumb_2);
                    LH.SetThumbOffset(thumb_offset_L[2], VRTRIXBones.L_Thumb_3);
                    LH.SetThumbSlerpRate(thumb_proximal_slerp, thumb_middle_slerp);
                    LH.RegisterCallBack();
                    LH.StartStreaming();
                }
                RH_Mode = RH.Init(HANDTYPE.RIGHT_HAND);
                if (RH_Mode)
                {
                    print("Right hand glove connected!");
                    RH.SetThumbOffset(thumb_offset_R[0], VRTRIXBones.R_Thumb_1);
                    RH.SetThumbOffset(thumb_offset_R[1], VRTRIXBones.R_Thumb_2);
                    RH.SetThumbOffset(thumb_offset_R[2], VRTRIXBones.R_Thumb_3);
                    RH.SetThumbSlerpRate(thumb_proximal_slerp, thumb_middle_slerp);
                    RH.RegisterCallBack();
                    RH.StartStreaming();
                }

            }
            catch (Exception e)
            {
                print("Exception caught: " + e);
            }
        }
        
        //数据手套反初始化，硬件断开连接
        //! Disconnect data glove and uninitialization.
        public void OnDisconnectGlove()
        {
            if (LH_Mode)
            {
                if (LH.ClosePort())
                {
                    LH = new VRTRIXDataWrapper(AdvancedMode, (int)Index, version);
                }
                LH_Mode = false;
            }
            if (RH_Mode)
            {
                if (RH.ClosePort())
                {
                    RH = new VRTRIXDataWrapper(AdvancedMode, (int)Index, version);
                }
                RH_Mode = false;
            }
        }

        //数据手套硬件地磁校准数据储存，仅在磁场大幅度变化后使用。
        //! Save hardware calibration parameters in IMU, only used in magnetic field changed dramatically.
        public void OnHardwareCalibrate()
        {
            if (LH_Mode)
            {
                LH.OnSaveCalibration();
            }
            if (RH_Mode)
            {
                RH.OnSaveCalibration();
            }
        }

        //数据手套振动
        //! Trigger a haptic vibration on data glove.
        public void OnVibrate()
        {
            if (LH_Mode)
            {
                LH.VibratePeriod(500);
            }
            if (RH_Mode)
            {
                RH.VibratePeriod(500);
            }
        }

        //数据手套手动跳频
        //! Switch radio channel of data glove. Only used for testing/debuging. Automatic channel switching is enabled by default in normal mode.
        public void OnChannelHopping()
        {
            if (LH_Mode)
            {
                LH.ChannelHopping();
            }
            if (RH_Mode)
            {
                RH.ChannelHopping();
            }
        }


        //数据手套五指张开解锁
        //! Activate advanced mode so that finger's yaw data will be unlocked.
        /*! 
         * \param bIsAdvancedMode Advanced mode will be activated if set to true.
         */
        public void SetAdvancedMode(bool bIsAdvancedMode)
        {
            if (LH_Mode)
            {
                LH.SetAdvancedMode(bIsAdvancedMode);
            }
            if (RH_Mode)
            {
                RH.SetAdvancedMode(bIsAdvancedMode);
            }
        }

        //改变数据手套硬件版本
        //! Set data gloves hardware version.
        /*! 
         * \param version Data glove hardware version.
         */
        public void SetHardwareVersion(GLOVEVERSION version)
        {
            if (LH_Mode)
            {
                LH.SetHardwareVersion(version);
            }
            if (RH_Mode)
            {
                RH.SetHardwareVersion(version);
            }
        }

        //数据手套设置手背初始方向。
        //! Align five fingers to closed gesture (only if advanced mode is set to true). Also align wrist to the game object chosen.
        public void OnAlignWrist()
        {
            if (LH_Mode)
            {
                qloffset = CalculateStaticOffset(LH, HANDTYPE.LEFT_HAND);
            }
            if (RH_Mode)
            {
                qroffset = CalculateStaticOffset(RH, HANDTYPE.RIGHT_HAND);
            }
        }

        //数据手套软件对齐四指。
        //! Align five fingers to closed gesture (only if advanced mode is set to true). Also align wrist to the game object chosen.
        public void OnAlignFingers()
        {
            if (LH_Mode)
            {
                LH.OnCloseFingerAlignment(HANDTYPE.LEFT_HAND);
            }
            if (RH_Mode)
            {
                RH.OnCloseFingerAlignment(HANDTYPE.RIGHT_HAND);
            }
        }

        //程序退出
        //! Application quit operation. 
        void OnApplicationQuit()
        {
            if (LH_Mode && LH.GetReceivedStatus() != VRTRIXGloveStatus.CLOSED)
            {
                LH.ClosePort();
            }
            if (RH_Mode && RH.GetReceivedStatus() != VRTRIXGloveStatus.CLOSED)
            {
                RH.ClosePort();
            }
        }

        //! Get current rotation of specific joint
        /*! 
         * \param bone specific joint of hand.
         * \return current rotation of specific joint.
         */
        public Quaternion GetRotation(VRTRIXBones bone)
        {
            return fingerTransformArray[(int)bone].rotation;
        }

        //获取磁场校准水平，值越小代表效果越好
        //! Get current calibration score for specific IMU sensor
        /*! 
         * \param bone specific joint of hand.
         * \return current calibration score for specific IMU sensor. Lower value of score means better calibration performance.
         */
        public int GetCalScore(VRTRIXBones bone)
        {
            if ((int)bone < 16)
            {
                return RH.GetReceivedCalScore(bone);
            }
            else
            {
                return LH.GetReceivedCalScore(bone);
            }
        }

        //获取信号强度，值越大代表信号越强
        //! Get radio strength of data glove 
        /*! 
         * \param type Data glove hand type.
         * \return radio strength of data glove. Higher value of score means better radio strength.         
         */
        public int GetReceiveRadioStrength(HANDTYPE type)
        {
            switch (type)
            {
                case HANDTYPE.RIGHT_HAND:
                    {
                        return RH.GetReceiveRadioStrength();
                    }
                case HANDTYPE.LEFT_HAND:
                    {
                        return LH.GetReceiveRadioStrength();
                    }
                default:
                    return 0;
            }
        }

        //获取当前通信信道，1-100共100个信道
        //! Get current radio channel of data glove used
        /*! 
         * \param type Data glove hand type.
         * \return current radio channel of data glove used.         
         */
        public int GetReceiveRadioChannel(HANDTYPE type)
        {
            switch (type)
            {
                case HANDTYPE.RIGHT_HAND:
                    {
                        return RH.GetReceiveRadioChannel();
                    }
                case HANDTYPE.LEFT_HAND:
                    {
                        return LH.GetReceiveRadioChannel();
                    }
                default:
                    return 0;
            }
        }
        //获取电量
        //! Get current battery level in percentage of data glove
        /*! 
         * \param type Data glove hand type.
         * \return current battery level in percentage of data glove.         
         */
        public float GetBatteryLevel(HANDTYPE type)
        {
            switch (type)
            {
                case HANDTYPE.RIGHT_HAND:
                    {
                        return RH.GetReceiveBattery();
                    }
                case HANDTYPE.LEFT_HAND:
                    {
                        return LH.GetReceiveBattery();
                    }
                default:
                    return 0;
            }
        }
        //获取磁场校准水平均值
        //! Get current calibration score average value
        /*! 
         * \param type Data glove hand type.
         * \return current calibration score average value.
         */
        public int GetReceivedCalScoreMean(HANDTYPE type)
        {
            switch (type)
            {
                case HANDTYPE.RIGHT_HAND:
                    {
                        return RH.GetReceivedCalScoreMean();
                    }
                case HANDTYPE.LEFT_HAND:
                    {
                        return LH.GetReceivedCalScoreMean();
                    }
                default:
                    return 0;
            }
        }

        //获取实际帧率
        //! Get data rate received per second 
        /*! 
         * \param type Data glove hand type.
         * \return data rate received per second.         
         */
        public int GetReceivedDataRate(HANDTYPE type)
        {
            switch (type)
            {
                case HANDTYPE.RIGHT_HAND:
                    {
                        return RH.GetReceivedDataRate();
                    }
                case HANDTYPE.LEFT_HAND:
                    {
                        return LH.GetReceivedDataRate();
                    }
                default:
                    return 0;
            }
        }

        //获取连接状态
        //! Get data glove connection status 
        /*! 
         * \param type Data glove hand type.
         * \return data glove connection status.         
         */
        public bool GetGloveConnectionStat(HANDTYPE type)
        {
            return GetReceivedStatus(type) == VRTRIXGloveStatus.NORMAL;
        }

        //! Get data glove status 
        /*! 
         * \param type Data glove hand type.
         * \return data glove status.         
         */
        public VRTRIXGloveStatus GetReceivedStatus(HANDTYPE type)
        {
            switch (type)
            {
                case HANDTYPE.RIGHT_HAND:
                    {
                        return RH.GetReceivedStatus();
                    }
                case HANDTYPE.LEFT_HAND:
                    {
                        return LH.GetReceivedStatus();
                    }
                default:
                    return VRTRIXGloveStatus.CLOSED;
            }
        }
        
        //获取姿态
        //! Get the gesture detected
        /*! 
         * \param type Data glove hand type.
         * \return the gesture detected.         
         */
        public VRTRIXGloveGesture GetGesture(HANDTYPE type)
        {
            if (type == HANDTYPE.LEFT_HAND && LH_Mode)
            {
                return LH_Gesture;
            }
            else if (type == HANDTYPE.RIGHT_HAND && RH_Mode)
            {
                return RH_Gesture;
            }
            else
            {
                return VRTRIXGloveGesture.BUTTONNONE;
            }
        }

        //用于计算初始化物体的姿态和手背姿态（由数据手套得到）之间的四元数差值，该方法为静态调用，即只在初始化的时候调用一次，之后所有帧均使用同一个四元数。
        //适用于：当动捕设备没有腕关节/手背节点或者只单独使用手套，无其他定位硬件设备时。
        private Quaternion CalculateStaticOffset(VRTRIXDataWrapper glove, HANDTYPE type)
        {
            if (type == HANDTYPE.RIGHT_HAND)
            {
                if (IsVREnabled)
                {
                    float angle_offset = RH_tracker.transform.rotation.eulerAngles.z;
                    return Quaternion.AngleAxis(-angle_offset, Vector3.forward); 
                }
                else
                {
                    Quaternion rotation = glove.GetReceivedRotation(VRTRIXBones.R_Hand);
                    Vector3 quat_vec = mr_axisoffset.MultiplyVector(new Vector3(rotation.x, rotation.y, rotation.z));
                    rotation = new Quaternion(quat_vec.x, quat_vec.y, quat_vec.z, rotation.w);
                    return RH_ObjectToAlign.transform.rotation * Quaternion.Inverse(rotation);
                }
            }
            else if (type == HANDTYPE.LEFT_HAND)
            {
                if (IsVREnabled)
                {
                    float angle_offset = LH_tracker.transform.rotation.eulerAngles.z;
                    return Quaternion.AngleAxis(-angle_offset, Vector3.forward); 
                }
                else
                {
                    Quaternion rotation = glove.GetReceivedRotation(VRTRIXBones.L_Hand);
                    Vector3 quat_vec = ml_axisoffset.MultiplyVector(new Vector3(rotation.x, rotation.y, rotation.z));
                    rotation = new Quaternion(quat_vec.x, quat_vec.y, quat_vec.z, rotation.w);
                    return LH_ObjectToAlign.transform.rotation * Quaternion.Inverse(rotation);
                }
            }
            else
            {
                return Quaternion.identity;
            }
        }

        //用于计算左手/右手腕关节姿态（由动捕设备得到）和左手手背姿态（由数据手套得到）之间的四元数差值，该方法为动态调用，即每一帧都会调用该计算。
        //适用于：当动捕设备有腕关节/手背节点时
        private Quaternion CalculateDynamicOffset(GameObject tracker, VRTRIXDataWrapper glove, HANDTYPE type)
        {
            //计算场景中角色右手腕在unity世界坐标系下的旋转与手套的右手腕在手套追踪系统中世界坐标系下右手腕的旋转之间的角度差值，意在匹配两个坐标系的方向；
            if (type == HANDTYPE.RIGHT_HAND)
            {
                Quaternion rotation = glove.GetReceivedRotation(VRTRIXBones.R_Hand);
                Vector3 quat_vec = mr_axisoffset.MultiplyVector(new Vector3(rotation.x, rotation.y, rotation.z));
                rotation = new Quaternion(quat_vec.x, quat_vec.y, quat_vec.z, rotation.w);
                Quaternion target =  tracker.transform.rotation * qroffset * Quaternion.Euler(0, -90, 90); 
                return target * Quaternion.Inverse(rotation);
            }

            //计算场景中角色左手腕在unity世界坐标系下的旋转与手套的左手腕在手套追踪系统中世界坐标系下左手腕的旋转之间的角度差值，意在匹配两个坐标系的方向；
            else if (type == HANDTYPE.LEFT_HAND)
            {
                Quaternion rotation = glove.GetReceivedRotation(VRTRIXBones.L_Hand);
                Vector3 quat_vec = ml_axisoffset.MultiplyVector(new Vector3(rotation.x, rotation.y, rotation.z));
                rotation = new Quaternion(quat_vec.x, quat_vec.y, quat_vec.z, rotation.w);
                Quaternion target =  tracker.transform.rotation * qloffset * Quaternion.Euler(0, 90, -90);
                return target * Quaternion.Inverse(rotation);
            }
            else
            {
                return Quaternion.identity;
            }
        }
        
        //手腕关节位置赋值函数，通过手腕外加的定位物体位置计算手部关节位置。（如果模型为全身骨骼，无需使用该函数）
        private void SetPosition(VRTRIXBones bone, Vector3 pos, Quaternion rot, Vector3 offset)
        {
            Transform obj = fingerTransformArray[(int)bone];
            if (obj != null)
            {
                obj.position = pos + rot* offset;
            }
        }

        //手部关节旋转赋值函数，每一帧都会调用，通过从数据手套硬件获取当前姿态，进一步进行处理，然后给模型赋值。
        private void SetRotation(VRTRIXBones bone, Quaternion rotation, HANDTYPE type)
        {
            Transform obj = fingerTransformArray[(int)bone];
            if (obj != null)
            {
                if (!float.IsNaN(rotation.x) && !float.IsNaN(rotation.y) && !float.IsNaN(rotation.z) && !float.IsNaN(rotation.w))
                {
                    if (type == HANDTYPE.LEFT_HAND)
                    {
                        Vector3 quat_vec = ml_axisoffset.MultiplyVector(new Vector3(rotation.x, rotation.y, rotation.z));
                        rotation = new Quaternion(quat_vec.x, quat_vec.y, quat_vec.z, rotation.w);
                        if (IsVREnabled)
                        {
                            //当VR环境下，根据固定在手腕上tracker的方向对齐手背方向。
                            obj.rotation = (bone == VRTRIXBones.L_Hand) ? CalculateDynamicOffset(LH_tracker, LH, HANDTYPE.LEFT_HAND) * rotation :
                                                                     CalculateDynamicOffset(LH_tracker, LH, HANDTYPE.LEFT_HAND)* rotation * Quaternion.Euler(ql_modeloffset);
                        }
                        else
                        {
                            //当3D环境下，根据相机视角方向对齐手背方向。
                            obj.rotation = (bone == VRTRIXBones.L_Hand) ? qloffset * rotation :
                                                                     qloffset * rotation * Quaternion.Euler(ql_modeloffset);
                        }
                    }
                    else if (type == HANDTYPE.RIGHT_HAND)
                    {
                        Vector3 quat_vec = mr_axisoffset.MultiplyVector(new Vector3(rotation.x, rotation.y, rotation.z));
                        rotation = new Quaternion(quat_vec.x, quat_vec.y, quat_vec.z, rotation.w);
                        if (IsVREnabled)
                        {
                            obj.rotation = (bone == VRTRIXBones.R_Hand) ? CalculateDynamicOffset(RH_tracker, RH, HANDTYPE.RIGHT_HAND)* rotation :
                                                                            CalculateDynamicOffset(RH_tracker, RH, HANDTYPE.RIGHT_HAND)* rotation * Quaternion.Euler(qr_modeloffset);
                        }
                        else
                        {
                            obj.rotation = (bone == VRTRIXBones.R_Hand) ? qroffset * rotation :
                                                                     qroffset * rotation * Quaternion.Euler(qr_modeloffset);
                        }
                    }
                }
            }
        }
        private Transform[] FindFingerTransform()
        {
            Transform[] transform_array = new Transform[(int)VRTRIXBones.NumOfBones];
            for(int i = 0; i < (int)VRTRIXBones.NumOfBones; ++i)
            {
                string bone_name = VRTRIXUtilities.GetBoneName(i);
                if (GetComponent("VRTRIXBoneMapping"))
                {
                    GameObject bone = VRTRIXBoneMapping.UniqueStance.MapToVRTRIX_BoneName(bone_name);
                    if(bone != null)
                    {
                        transform_array[i] = bone.transform;
                    }
                    //print(bone);
                }
            }
            return transform_array;
        }
        
        //! Check the tracked device model name stored in hardware config to find specific hardware type. (SteamVR Tracking support)
        /*! 
         * \param type Hand type to check(if wrist tracker for data glove is the hardware to check).
         * \param device Device type to check(if other kind of interactive hardware to check).
         * \return the gameobject of the tracked device.
         */
        public static GameObject CheckDeviceModelName(HANDTYPE type = HANDTYPE.NONE, InteractiveDevice device = InteractiveDevice.NONE)
        {
            var system = OpenVR.System;
            if (system == null)
                return null;
            for (int i = 0; i < 16; i++)
            {
                var error = ETrackedPropertyError.TrackedProp_Success;
                var capacity = system.GetStringTrackedDeviceProperty((uint)i, ETrackedDeviceProperty.Prop_RenderModelName_String, null, 0, ref error);
                if (capacity <= 1)
                {
                    continue;
                }

                var buffer = new System.Text.StringBuilder((int)capacity);
                system.GetStringTrackedDeviceProperty((uint)i, ETrackedDeviceProperty.Prop_RenderModelName_String, buffer, capacity, ref error);
                var s = buffer.ToString();
                if (type == HANDTYPE.LEFT_HAND)
                {
                    if (s.Contains("LH"))
                    {
                        return GameObject.Find("Device" + i);
                    }
                }
                else if (type == HANDTYPE.RIGHT_HAND)
                {
                    if (s.Contains("RH"))
                    {
                        return GameObject.Find("Device" + i);
                    }
                }

                else if(device == InteractiveDevice.SWORD)
                {
                    if (s.Contains("sword"))
                    {
                        GameObject sword_ref = GameObject.Find("Device" + i);
                        sword_ref.GetComponent<SteamVR_RenderModel>().enabled = false;
                        return sword_ref;
                    }
                }
                else if (device == InteractiveDevice.SHIELD)
                {
                    if (s.Contains("shield"))
                    {
                        GameObject shield_ref = GameObject.Find("Device" + i);
                        shield_ref.GetComponent<SteamVR_RenderModel>().enabled = false;
                        return shield_ref;
                    }
                }
                else if (device == InteractiveDevice.EXTINGUISHER)
                {
                    if (s.Contains("fire"))
                    {
                        GameObject ex_ref = GameObject.Find("Device" + i);
                        return ex_ref;
                    }
                }
            }
            return null;
        }
    }
}



