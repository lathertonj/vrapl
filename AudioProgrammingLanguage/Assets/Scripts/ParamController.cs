﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LanguageObject))]
public class ParamController : MonoBehaviour , ILanguageObjectListener, IControllerInputAcceptor
{
    public GameObject myText;
    public GameObject myShape;

    private string myStorageClass;
    private string myExitEvent;
    private int myNumChildren = 0;
    private string myParam;
    private int myParamIndex = 0;
    private bool amConnected = false;

    private ILanguageObjectListener myParent = null;
    private IParamAcceptor myParamAcceptor = null;

    // Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {

	}

    private void SwitchColors()
    {
        Color tempColor = myText.GetComponent<TextMesh>().color;
        myText.GetComponent<TextMesh>().color = myShape.GetComponent<Renderer>().material.color;
        myShape.GetComponent<Renderer>().material.color = tempColor;
    }

    public bool AcceptableChild( LanguageObject other, Collider mine )
    {
        // todo: accept others?
        if( other.GetComponent<NumberProducer>() != null ||
            other.GetComponent<SoundProducer>() != null )
        {
            return true;
        }

        return false;
    }

    public void NewParent( LanguageObject parent )
    {
        IParamAcceptor parentParamAcceptor = (IParamAcceptor) parent.GetComponent( typeof(IParamAcceptor) );
        if( parentParamAcceptor != null )
        {
            myParamAcceptor = parentParamAcceptor;
            myParamIndex = 0;
            myParam = myParamAcceptor.AcceptableParams()[myParamIndex];
            myText.GetComponent<TextMesh>().text = myParam;
            SwitchColors();
            myParent = (ILanguageObjectListener) parent.GetComponent( typeof(ILanguageObjectListener) );
        }
    }

    public void ParentDisconnected( LanguageObject parent )
    {
        if( myParamAcceptor != null )
        {
            SwitchColors();
            myParam = "param";
            myText.GetComponent<TextMesh>().text = myParam;
            myParamAcceptor = null;
            myParent = null;
        }
    }

    public void NewChild( LanguageObject child, Collider mine )
    {
        myNumChildren++;
        // if I got my first child after I was already hooked up to a chuck,
        // I need to hook up myself now.
        if( myNumChildren == 1 && myParamAcceptor != null && GetChuck() != null )
        {
            myParamAcceptor.ConnectParam( myParam, OutputConnection() );
            amConnected = true;
        }
    }
    
    public void ChildDisconnected( LanguageObject child )
    {
        myNumChildren--;
        // if I lost my last child while I was hooked up to a chuck,
        // I need to disconnect myself now
        if( myNumChildren == 0 && myParamAcceptor != null && GetChuck() != null )
        {
            myParamAcceptor.DisconnectParam( myParam, OutputConnection() );
            amConnected = false;
        }
    }

    public string InputConnection()
    {
        return string.Format("{0}.myGain", myStorageClass);
    }

    public string OutputConnection()
    {
        return InputConnection();
    }

    public void GotChuck( ChuckInstance chuck )
    {
        myStorageClass = chuck.GetUniqueVariableName();
        myExitEvent = chuck.GetUniqueVariableName();

        chuck.RunCode(string.Format(@"
            external Event {1};
            public class {0}
            {{
                static Gain @ myGain;
            }}
            Gain g @=> {0}.myGain;
            {0}.myGain => blackhole;

            // wait until told to exit
            {1} => now;
        ", myStorageClass, myExitEvent));

        if( myNumChildren > 0 && myParamAcceptor != null )
        {
            // if I already have some children when I get a chuck, I need to hook myself up
            myParamAcceptor.ConnectParam( myParam, OutputConnection() );
            amConnected = true;
        }
    }

    public void LosingChuck(ChuckInstance chuck)
    {
        if( myNumChildren > 0 && myParamAcceptor != null )
        {
            // if I still hve some children when I lose chuck, I need to disconnect myself
            myParamAcceptor.DisconnectParam( myParam, OutputConnection() );
            amConnected = false;
        }

        chuck.BroadcastEvent( myExitEvent );
    }

    public ChuckInstance GetChuck()
    {
        return GetComponent<LanguageObject>().GetChuck();
    }

    public string GetParamName()
    {
        return string.Format( "{0}'s {1}", myParent.VisibleName(), myParam );
    }

    public void TouchpadDown()
    {
        if( myParamAcceptor != null )
        {
            // only disconnect and reconnect if I was already connected!
            bool shouldDisconnectAndReconnect = amConnected;
            if( shouldDisconnectAndReconnect ) myParamAcceptor.DisconnectParam( myParam, OutputConnection() );

            // change my param and my text
            myParamIndex++;
            myParamIndex %= myParamAcceptor.AcceptableParams().Length;
            myParam = myParamAcceptor.AcceptableParams()[myParamIndex];
            myText.GetComponent<TextMesh>().text = myParam;

            // if I was already connected before, I need to connect this new param
            if( shouldDisconnectAndReconnect ) myParamAcceptor.ConnectParam( myParam, OutputConnection() );
        }
    }

    public void TouchpadUp()
    {
        // don't care
    }

    public void TouchpadAxis(Vector2 pos)
    {
        // don't care
    }

    public void TouchpadTransform( Transform t )
    {
        // don't care
    }
    
    public string VisibleName()
    {
        return myText.GetComponent<TextMesh>().text;
    }
}
