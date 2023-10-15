type RelesType = {
  rules: [
    {
      enabled: boolean;
      name: string;
      type: "Lifecycle";
      definition: {
        actions: {
          // NOTE: Delete is the only action available for all blob types; snapshots cannot auto set to hot
          version?: RuleAction;
          /* blobBlock */ baseBlob?: RuleAction;
          snapshopt?: Omit<RuleAction, "enableAutoTierToHotFromCool">;
          appendBlob?: { delete: ActionRunCondition }; // only one lifecycle policy
        };
        filters?: {
          blobTypes: Array<"appendBlob" | "blockBlob">;
          // A prefix string must start with a container name.
          // To match the container or blob name exactly, include the trailing forward slash ('/'), e.g., 'sample-container/' or 'sample-container/blob1/'
          // To match the container or blob name pattern (wildcard), omit the trailing forward slash, e.g., 'sample-container' or 'sample-container/blob1'
          prefixMatch?: string[];
          // Each rule can define up to 10 blob index tag conditions.
          // example, if you want to match all blobs with `Project = Contoso``: `{"name": "Project","op": "==","value": "Contoso"}``
          // https://learn.microsoft.com/en-us/azure/storage/blobs/storage-manage-find-blobs?tabs=azure-portal
          blobIndexMatch?: Record<string, string>;
        };
      };
    }
  ];
};

type RuleAction = {
  tierToCool?: ActionRunCondition;
  tierToArchive?: {
    daysAfterModificationGreaterThan: number;
    daysAfterLastTierChangeGreaterThan: number;
  };
  enableAutoTierToHotFromCool?: ActionRunCondition;
  delete?: ActionRunCondition;
};

type ActionRunCondition = {
  daysAfterModificationGreaterThan: number;
  daysAfterCreationGreaterThan: number;
  daysAfterLastAccessTimeGreaterThan: number; // requires last access time tracking
};