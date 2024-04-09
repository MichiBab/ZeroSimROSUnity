using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZO.ROS.MessageTypes;
using ZO.ROS.MessageTypes.Geometry;
using ZO.ROS.MessageTypes.Nav;
using ZO.ROS.MessageTypes.Std;
using ZO.ROS;
using ZO.ROS.Unity;
using ZO.Util;
using ZO.Physics;
using ZO.Document;
using System.Threading.Tasks;

public class TTSDummy : ZOROSUnityGameObjectBase
{


    /*TTS and play mp3 interface dummy*/

    public override string Type
    {
        get
        {
            return "controller.tts_audio_dummy";
        }
    }

    void OnValidate()
    {
        // make sure we have a name
        if (string.IsNullOrEmpty(Name))
        {
            Name = gameObject.name + "_" + Type;
        }

    }

    protected override void ZOAwake()
    {
        base.ZOAwake();

        // make sure we have a name
        if (string.IsNullOrEmpty(Name))
        {
            Name = gameObject.name + "_" + Type;
        }

    }



    protected override void ZOFixedUpdate()
    {

    }

    protected override void ZOFixedUpdateHzSynchronized()
    {
        if (!ZOROSBridgeConnection.Instance.IsConnected)
        {
            return;
        }
    }




    public string _ROSTTSTopic = "/voice/text_to_speech";
    public string _ROSTTSPubTopic = "/voice/text_to_speech";
    private StringMessage _ttsMessage = new StringMessage();


    public override void OnROSBridgeConnected(ZOROSUnityManager rosUnityManager)
    {
        Debug.Log("INFO: ZODifferentialDriveController::OnROSBridgeConnected");
        // subscribe to Twist Message
        ZOROSBridgeConnection.Instance.Subscribe<StringMessage>(Name, _ROSTTSTopic, _ttsMessage.MessageType, OnROSMessageReceived);
        ZOROSBridgeConnection.Instance.Advertise(_ROSTTSPubTopic, _ttsMessage.MessageType);

    }

    public override void OnROSBridgeDisconnected(ZOROSUnityManager rosUnityManager)
    {
        ZOROSBridgeConnection.Instance.UnAdvertise(_ROSTTSTopic);
        ZOROSBridgeConnection.Instance.UnAdvertise(_ROSTTSPubTopic);
        Debug.Log("INFO: ZODifferentialDriveController::OnROSBridgeDisconnected");
    }

    private void threadTTSPublisherRoutine(string msg)
    {
        //For each word in the msg, sleep one second
        //Publish speech started
        StringMessage speechStarted = new StringMessage("SpeechStarted: " + msg);
        ZOROSBridgeConnection.Instance.Publish<StringMessage>(_ROSTTSPubTopic, speechStarted);
        string[] words = msg.Split(' ');
        foreach (string word in words)
        {
            System.Threading.Thread.Sleep(1000);
        }
        //Publish done
        StringMessage speechDone = new StringMessage();
        speechDone.data = "SpeechFinished: " + msg;
        ZOROSBridgeConnection.Instance.Publish<StringMessage>(_ROSTTSPubTopic, speechDone);
    }



    public Task OnROSMessageReceived(ZOROSBridgeConnection rosBridgeConnection, ZOROSMessageInterface msg)
    {
        StringMessage multiArray = (StringMessage)msg;
        //Start a new thread to publish the speech
        System.Threading.Thread thread = new System.Threading.Thread(() => threadTTSPublisherRoutine(multiArray.data));
        thread.Start();

        return Task.CompletedTask;
    }


}


