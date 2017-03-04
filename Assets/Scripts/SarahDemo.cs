﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AssetManagerPackage;
using Assets.Scripts;
using IntegratedAuthoringTool;
using IntegratedAuthoringTool.DTOs;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WellFormedNames;
using RolePlayCharacter;
using UnityEngine.SceneManagement;
using Utilities;

public class SarahDemo : SingleCharacterDemo
{

	string groupName;
	int questionNumber = 0;
	string userReply;



	// Use this for initialization
	private IEnumerator Start ()
	{
		Initialized = false;
		_finalScore = GameObject.FindGameObjectWithTag ("FinalScore");
		_finalScore.SetActive (false);
		AssetManager.Instance.Bridge = new AssetManagerBridge ();
		;
		m_dialogController.AddDialogLine ("Loading...");

		alreadyUsedDialogs = new Dictionary<string, string> ();

		var streamingAssetsPath = Application.streamingAssetsPath;
#if UNITY_EDITOR || UNITY_STANDALONE
		streamingAssetsPath = "file://" + streamingAssetsPath;
#endif

		var www = new WWW (streamingAssetsPath + "/scenarioList.txt");
		yield return www;


		//removing the start button
		//FIXME
		//this.StartVersion (0);

		//        Time.timeScale = 0;

		if (!string.IsNullOrEmpty (www.error)) {
			m_dialogController.AddDialogLine ("Error: " + www.error);
			yield break;
		}

		var entries = www.text.Split (new[] { "\n", "\r\n" }, StringSplitOptions.None);
		if ((entries.Length % 2) != 0) {
			m_dialogController.AddDialogLine ("Error: Scenario entries must in groups of 2, to identify the scenario file, and TTS directory");
			yield break;
		}

		{
			List<ScenarioData> data = new List<ScenarioData> ();

			for (int i = 0; i < entries.Length; i += 2) {
				var path = entries [i].Trim ();
				var tts = entries [i + 1].Trim ();
				data.Add (new ScenarioData (path, tts));
			}

			m_scenarios = data.ToArray ();
		}

		m_dialogController.Clear ();
		LoadScenarioMenu ();
	}


	private void LoadScenarioMenu ()
	{
		ClearButtons ();
		foreach (var s in m_scenarios) {
			var data = s;
			AddButton (s.IAT.ScenarioName, () => {
				LoadScenario (data);
			});
		}
	}

	private void AddButton (string label, UnityAction action)
	{
		var button = Instantiate (m_menuButtonArchetype);
		var t = button.transform;
		t.SetParent (m_menuButtonHolder);
		t.localScale = Vector3.one;
		button.image.color = new Color (0, 0, 0, 0);
		button.image.color = new Color (200, 200, 200, 0);

		var buttonLabel = button.GetComponentInChildren<Text> ();
		buttonLabel.text = label;
		buttonLabel.color = Color.white;
		button.onClick.AddListener (action);
		m_currentMenuButtons.Add (button);
	}

	private void ClearButtons ()
	{
		foreach (var b in m_currentMenuButtons) {
			Destroy (b.gameObject);
		}
		m_currentMenuButtons.Clear ();
	}

	private void LoadScenario (ScenarioData data)
	{
		ClearButtons ();

		_iat = data.IAT;

		_introPanel.SetActive (true);
		_introText.text = string.Format ("<b>{0}</b>\n\n\n{1}", _iat.ScenarioName, _iat.ScenarioDescription);

		var characterSources = _iat.GetAllCharacterSources ().ToList ();
		foreach (var source in characterSources) {
			var rpc = RolePlayCharacterAsset.LoadFromFile (source.Source);
			rpc.LoadAssociatedAssets ();
			_iat.BindToRegistry (rpc.DynamicPropertiesRegistry);
			AddButton (characterSources.Count == 1 ? "Start" : rpc.CharacterName.ToString (),
				() => {
					var body = m_bodies.FirstOrDefault (b => b.BodyName == rpc.BodyName);
					_agentController = new AgentControler (data, rpc, _iat, body.CharaterArchtype, m_characterAnchor, m_dialogController);
					StopAllCoroutines ();
					_agentController.storeFinalScore (_finalScore);
					_agentController.Start (this, VersionMenu);
					InstantiateScore ();
				});
		}
		AddButton ("Back to Scenario Selection Menu", () => {
			_iat = null;
			LoadScenarioMenu ();
		});
	}

	public void SaveState ()
	{
		_agentController.SaveOutput ();
	}

	private void UpdateButtonTexts (bool hide, IEnumerable<DialogueStateActionDTO> dialogOptions)
	{

		if (hide) {
			if (!m_buttonList.Any ())
				return;
			foreach (var b in m_buttonList) {
				Destroy (b.gameObject);
			}
			m_buttonList.Clear ();
		} else {
			if (m_buttonList.Count == dialogOptions.Count ())
				return;

			foreach (var d in dialogOptions) {
				var id = d.Id;
				//bypass start
				if (d.Utterance == "START") {
					Reply (id);
				} else {
					var b = Instantiate (m_dialogButtonArchetype);
					var t = b.transform;
					t.SetParent (m_dialogButtonZone, false);

					//b.GetComponentInChildren<Text>().color = Color.yellow;
					//GameObject.Find("DialogButton(Clone)").GetComponent <Text>().color = Color.yellow;

					b.GetComponentInChildren<Text> ().text = d.Utterance;

					b.GetComponentInChildren<Text> ().verticalOverflow = VerticalWrapMode.Overflow;

					b.GetComponentInChildren<Text> ().resizeTextMaxSize = 12;

					//This doesn't change the color of the text to yellow?!
					//b.GetComponentInChildren<Text>().color = Color.yellow;


					b.onClick.AddListener ((() => Reply (d.Id)));
					m_buttonList.Add (b);

					GameObject.Find ("MenuZone").GetComponent<Image> ().enabled = true;
				}
			}
		}
	}

	public void Reply (Guid dialogId)
	{

		//recording Group Name
		groupName = GameObject.Find ("InputField").GetComponent<InputField> ().text;

		var state = _agentController.RPC.GetBeliefValue ("DialogState(Player)");
		if (state == IATConsts.TERMINAL_DIALOGUE_STATE) {
			return;
		}
		var reply = _iat.GetDialogActionById (IATConsts.PLAYER, dialogId);


		///userReply = reply.Utterance;

		//make group number textbox invisible after group number entered
		questionNumber = questionNumber + 1;
		if (questionNumber == 2) {
			if (groupName == "") {
				questionNumber = questionNumber - 1;
				return;
			} else {

				GameObject.Find ("InputField").transform.localScale = new Vector3 (0, 0, 0);
				GameObject.Find ("Text").transform.localScale = new Vector3 (0, 0, 0);
			}
		}


		if (reply.Utterance == "BYE") {
			Application.Quit ();
		}


		var actionFormat = string.Format ("Speak({0},{1},{2},{3})", reply.CurrentState, reply.NextState, reply.Meaning, reply.Style);


		StartCoroutine (PlayerReplyAction (actionFormat, reply.NextState));
		UpdateScore (reply);

		alreadyUsedDialogs.Add (reply.Utterance, reply.UtteranceId);

	}

	private IEnumerator PlayerReplyAction (string replyActionName, string nextState)
	{
		const float WAIT_TIME = 0.5f;
		_agentController.AddEvent (EventHelper.ActionStart (IATConsts.PLAYER, replyActionName, _agentController.RPC.CharacterName.ToString ()).ToString ());
		yield return new WaitForSeconds (WAIT_TIME);
		_agentController.AddEvent (EventHelper.ActionEnd (IATConsts.PLAYER, replyActionName, _agentController.RPC.CharacterName.ToString ()).ToString ());
		_agentController.AddEvent (EventHelper.PropertyChanged (string.Format (IATConsts.DIALOGUE_STATE_PROPERTY, IATConsts.PLAYER), nextState, "SELF").ToString ());
		Initialized = true;

	}

	// Update is called once per frame
	void Update ()
	{
		if (_agentController == null)
			return;

		if (!_agentController.IsRunning)
			return;

		if (_agentController.getJustReplied ()) {
			UpdateScore (_agentController.getReply ());
		}

		if (Input.GetKeyDown (KeyCode.P)) {
			if (Time.timeScale > 0)
				Time.timeScale = 0;
			else
				Time.timeScale = 1;
		}

		if (Input.GetKeyDown (KeyCode.S)) {
			this.SaveState ();
		}

		_agentController.UpdateEmotionExpression ();

		var state = (Name)_agentController.RPC.GetBeliefValue (string.Format (IATConsts.DIALOGUE_STATE_PROPERTY, IATConsts.PLAYER));
		var possibleOptions = _iat.GetDialogueActionsByState (IATConsts.PLAYER, state.ToString ());

		var originalPossibleActions = possibleOptions;

		if (!possibleOptions.Any ()) {
			UpdateButtonTexts (true, null);
		} else {
			if (PJScenario) {
				if (!Initialized) {
					var newOptions = possibleOptions.Where (x => x.CurrentState == IATConsts.INITIAL_DIALOGUE_STATE).Take (3).Shuffle ().ToList ();

					newOptions.AddRange (_iat.GetDialogueActionsByState (IATConsts.PLAYER, "Introduction"));
					possibleOptions = newOptions;
				} else {
					var newOptions = possibleOptions.Where (x => !alreadyUsedDialogs.ContainsKey (x.Utterance)).Shuffle ().Take (3).ToList ();

					var additionalOptions = _iat.GetDialogueActionsByState (IATConsts.PLAYER, "Start")
                            .Where (x => !alreadyUsedDialogs.ContainsKey (x.Utterance) && !newOptions.Contains (x)).Shuffle ().Take (2);

					possibleOptions = newOptions.Concat (additionalOptions).Shuffle ();

					if (alreadyUsedDialogs.Count () > 12 && possibleOptions.Count () < 6) {
						var ClosureOptions = _iat.GetDialogueActionsByState (IATConsts.PLAYER, "Closure").Take (1).ToList ();

						possibleOptions = newOptions.Concat (additionalOptions).Concat (ClosureOptions).Shuffle ();
					}
				}
			}
		}
		UpdateButtonTexts (false, possibleOptions);
	}

	private void LateUpdate ()
	{
		if (_agentController != null)
			_agentController.UpdateFields ();
	}


	private string GetNextPJState (string currentState)
	{
		switch (currentState) {
		case "Start":
			return "FreeRecall";
		case "GreetingSpecific":
			return "Start";
		case "WorkSpecific":
			return "Start";
		case "WifeSpecific":
			return "Start";
		case "KidsSpecific":
			return "Start";
		case "Greeting":
			return "Start";
		case "CowSpecific":
			return "FreeRecall";
		case "FreeRecall":
			return "Questioning";
		case "Questioning":
			return "Closure";
		default:
			return "FreeRecall";
		}
	}

	private void InstantiateScore ()
	{

		score = Instantiate (ScoreTextPrefab);

		var t = score.transform;
		t.SetParent (m_scoreZone, false);

		if (PJScenario) {
			var obj = GameObject.FindGameObjectWithTag ("Score");
			obj.GetComponent<ScoreManager> ().SetPJ (true);
			obj.GetComponent<ScoreManager> ().Refresh ();

		}
	}

	public void UpdateScore (DialogueStateActionDTO reply)
	{


		foreach (var meaning in reply.Meaning) {

			HandleKeywords (meaning);
		}

		foreach (var style in reply.Style) {

			HandleKeywords (style);
		}
	}

	/* private IEnumerable<DialogueStateActionDTO> HandleContext(string s)
     {
         IEnumerable<DialogueStateActionDTO> ret =

     }*/


	private void HandleKeywords (string s)
	{

		char[] delimitedChars = { '(', ')' };

		string[] result = s.Split (delimitedChars);



		if (result.Length > 1)
		if (PJScenario) {
			switch (result [0]) {
			case "Aggression":
				score.GetComponent<ScoreManager> ().addAggression (Int32.Parse (result [1]));
				break;

			case "Information":
				score.GetComponent<ScoreManager> ().addInformation (Int32.Parse (result [1]));
				break;

			case "Truth":
				score.GetComponent<ScoreManager> ().addTruth (Int32.Parse (result [1]));
				break;

			}
		} else
			switch (result [0]) {
			case "Inquire":
				score.GetComponent<ScoreManager> ().AddI (Int32.Parse (result [1]));
				break;

			case "FAQ":
				score.GetComponent<ScoreManager> ().AddF (Int32.Parse (result [1]));
				break;

			case "Closure":
				score.GetComponent<ScoreManager> ().AddC (Int32.Parse (result [1]));
				break;

			case "Empathy":
				score.GetComponent<ScoreManager> ().AddE (Int32.Parse (result [1]));
				break;

			case "Polite":
				score.GetComponent<ScoreManager> ().AddP (Int32.Parse (result [1]));
				break;


			}

	}

	public void ClearScore ()
	{

		Destroy (score);
	}

	public void End ()
	{

		SceneManager.LoadScene (0);

	}

}