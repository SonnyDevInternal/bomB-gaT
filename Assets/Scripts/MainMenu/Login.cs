using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Login : MonoBehaviour
{
    static public string currentCookie = "";

    private string host = "http://localhost";

    [SerializeField] 
    private Button loginButton = null;
    [SerializeField] 
    private Button registerButton = null;

    [SerializeField] 
    private TMPro.TMP_InputField usernameField = null;
    [SerializeField] 
    private TMPro.TMP_InputField passwordField = null;

    //false  = done, true = working
    private bool loginState = false; 
    private bool registerState = false; 

    void Start()
    {
        loginButton.onClick.AddListener(OnClickLogin);
        registerButton.onClick.AddListener(OnClickRegister);
    }

    private void OnDestroy()
    {
        loginButton.onClick.RemoveAllListeners();
        registerButton.onClick.RemoveAllListeners();
    }

    void OnSuccessFullLogin(Dictionary<string, string> jsonBody)
    {
        if(jsonBody.TryGetValue("cookie", out string cookie))
        {
            Login.currentCookie = cookie;

            Debug.Log("Got Cookie bruh: " + cookie);

            SceneManager.LoadScene("MainGame");
        }
        else
        {
            Debug.LogError("Failed to retrieve cookie");
        }
    }

    void OnClickLogin()
    {
        string username = usernameField.text;
        string password = passwordField.text;

        if (isValidData(username, password))
        {
            if(!loginState && !registerState)
            {
                loginState = true;

                StartCoroutine(SendLoginRequest(username, password));
            }
            else
            {
                Debug.Log("An Operation is still processing!");
            }
        }
        else
        {
            Debug.LogError("Entered data was not completed!");
        }
    }

    void OnClickRegister()
    {
        string username = usernameField.text;
        string password = passwordField.text;

        if (isValidData(username, password))
        {
            if (!loginState && !registerState)
            {
                registerState = true;

                StartCoroutine(SendRegisterRequest(username, password));
            }
            else
            {
                Debug.Log("An Operation is still processing!");
            }
        }
        else
        {
            Debug.LogError("Entered data was not completed!");
        }
    }

    private IEnumerator<UnityWebRequestAsyncOperation> SendLoginRequest(string username, string password)
    {
        WWWForm form = new WWWForm();

        form.AddField("name", username);
        form.AddField("pw", password);

        using (UnityWebRequest request = UnityWebRequest.Post($"{host}/Login.php", form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<Dictionary<string, string>>(request.downloadHandler.text);

                    OnSuccessFullLogin(response);
                }
                catch (JsonException e)
                {
                    Debug.LogError("Failed to parse JSON: " + e.Message);
                }
            }
            else
            {

                try
                {
                    var response = JsonConvert.DeserializeObject<Dictionary<string, string>>(request.downloadHandler.text);

                    Debug.LogError("Login failed: " + request.error);
                }
                catch (JsonException e)
                {
                    Debug.LogError("Failed to parse JSON: " + e.Message);
                }
            }

            loginState = false;
        }
    }

    private IEnumerator<UnityWebRequestAsyncOperation> SendRegisterRequest(string username, string password)
    {
        WWWForm form = new WWWForm();

        form.AddField("name", username);
        form.AddField("pw", password);

        using (UnityWebRequest request = UnityWebRequest.Post($"{host}/Register.php", form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonConvert.DeserializeObject<Dictionary<string, string>>(request.downloadHandler.text);

                Debug.Log("Registered Successfully!");

                OnSuccessFullLogin(response);
            }
            else
            {
                var response = JsonConvert.DeserializeObject<Dictionary<string, string>>(request.downloadHandler.text);

                Debug.LogError("Registration failed: " + response["message"]);
            }

            registerState = false;
        }
    }

    private bool isValidData(string username, string password)
    {
        return !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);
    }
}
