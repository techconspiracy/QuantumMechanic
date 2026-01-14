#region Fields
        
        [Header("Dialogue State")]
        private DialogueState currentState = DialogueState.Inactive;
        private DialogueTree activeTree;
        private DialogueNode currentNode;
        private int currentLineIndex;
        
        [Header("Dialogue Data")]
        private Dictionary<string, DialogueTree> dialogueTrees = new Dictionary<string, DialogueTree>();
        private Dictionary<string, object> dialogueVariables = new Dictionary<string, object>();
        
        [Header("Settings")]
        [SerializeField] private float defaultTextSpeed = 0.05f;
        [SerializeField] private bool autoAdvance = false;
        [SerializeField] private float autoAdvanceDelay = 2f;
        
        #endregion
        
        #region Events
        
        public event Action<DialogueLine> OnLineDisplay;
        public event Action<List<DialogueChoice>> OnChoicesDisplay;
        public event Action OnDialogueStart;
        public event Action OnDialogueEnd;
        public event Action<string> OnVariableChanged;
        
        #endregion
        
        #region Initialization
        
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// Initialize the dialogue system
        /// </summary>
        private void Initialize()
        {
            dialogueTrees.Clear();
            dialogueVariables.Clear();
            currentState = DialogueState.Inactive;
            Debug.Log("[DialogueSystem] Initialized");
        }
        
        #endregion
        
        #region Dialogue Control
        
        /// <summary>
        /// Start a dialogue tree
        /// </summary>
        public void StartDialogue(string treeID)
        {
            if (!dialogueTrees.ContainsKey(treeID))
            {
                Debug.LogError($"[DialogueSystem] Dialogue tree '{treeID}' not found");
                return;
            }
            
            if (currentState != DialogueState.Inactive)
            {
                Debug.LogWarning("[DialogueSystem] Cannot start dialogue while another is active");
                return;
            }
            
            activeTree = dialogueTrees[treeID];
            currentState = DialogueState.Processing;
            currentLineIndex = 0;
            
            OnDialogueStart?.Invoke();
            
            if (activeTree.nodes.ContainsKey(activeTree.startNodeID))
            {
                ProcessNode(activeTree.startNodeID);
            }
            else
            {
                Debug.LogError($"[DialogueSystem] Start node '{activeTree.startNodeID}' not found");
                EndDialogue();
            }
        }
        
        /// <summary>
        /// End the current dialogue
        /// </summary>
        public void EndDialogue()
        {
            if (currentState == DialogueState.Inactive) return;
            
            currentNode?.onNodeExit?.Invoke();
            
            currentState = DialogueState.Inactive;
            activeTree = null;
            currentNode = null;
            currentLineIndex = 0;
            
            OnDialogueEnd?.Invoke();
            Debug.Log("[DialogueSystem] Dialogue ended");
        }
        
        #endregion
        
        #region Node Processing
        
        /// <summary>
        /// Process a dialogue node
        /// </summary>
        private void ProcessNode(string nodeID)
        {
            if (!activeTree.nodes.ContainsKey(nodeID))
            {
                Debug.LogError($"[DialogueSystem] Node '{nodeID}' not found");
                EndDialogue();
                return;
            }
            
            currentNode?.onNodeExit?.Invoke();
            
            currentNode = activeTree.nodes[nodeID];
            currentLineIndex = 0;
            
            currentNode.onNodeEnter?.Invoke();
            
            if (currentNode.endDialogue)
            {
                EndDialogue();
                return;
            }
            
            if (currentNode.lines.Count > 0)
            {
                DisplayLine(currentNode.lines[0]);
            }
            else if (currentNode.choices.Count > 0)
            {
                ShowChoices();
            }
            else if (!string.IsNullOrEmpty(currentNode.autoNextNodeID))
            {
                ProcessNode(currentNode.autoNextNodeID);
            }
            else
            {
                EndDialogue();
            }
        }
        
        /// <summary>
        /// Display a dialogue line
        /// </summary>
        private void DisplayLine(DialogueLine line)
        {
            currentState = DialogueState.Speaking;
            OnLineDisplay?.Invoke(line);
            
            Debug.Log($"[DialogueSystem] {line.speakerName}: {line.text}");
        }
        
        /// <summary>
        /// Advance to next line or choices
        /// </summary>
        public void AdvanceDialogue()
        {
            if (currentState != DialogueState.Speaking) return;
            
            currentLineIndex++;
            
            if (currentLineIndex < currentNode.lines.Count)
            {
                DisplayLine(currentNode.lines[currentLineIndex]);
            }
            else if (currentNode.choices.Count > 0)
            {
                ShowChoices();
            }
            else if (!string.IsNullOrEmpty(currentNode.autoNextNodeID))
            {
                ProcessNode(currentNode.autoNextNodeID);
            }
            else
            {
                EndDialogue();
            }
        }
        
        #endregion