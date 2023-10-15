function validateToDoItemTimestamp() {
  var context = getContext();
  var request = context.getRequest();

  // item to be created in the current operation
  var itemToCreate = request.getBody();

  // validate properties
  if (!("timestamp" in itemToCreate)) {
    var ts = new Date();
    itemToCreate["timestamp"] = ts.getTime();
  }

  // update the item that will be created
  request.setBody(itemToCreate);
}

// Posttrigger
function updateMetadata() {
  var context = getContext();
  var container = context.getCollection();
  var response = context.getResponse();

  // item that was created
  var createdItem = response.getBody();

  // query for metadata document
  var filterQuery = 'SELECT * FROM root r WHERE r.id = "_metadata"';
  var accept = container.queryDocuments(
    container.getSelfLink(),
    filterQuery,
    updateMetadataCallback
  );
  if (!accept) throw "Unable to update metadata, abort";

  function updateMetadataCallback(err, items, responseOptions) {
    if (err) throw new Error("Error" + err.message);
    if (items.length != 1) throw "Unable to find metadata document";

    var metadataItem = items[0];

    // update metadata
    metadataItem.createdItems += 1;
    metadataItem.createdNames += " " + createdItem.id;
    var accept = container.replaceDocument(
      metadataItem._self,
      metadataItem,
      function (err, itemReplaced) {
        if (err) throw "Unable to update metadata, abort";
      }
    );
    if (!accept) throw "Unable to update metadata, abort";
    return;
  }
}

function tax(income) {
  if (income == undefined) throw "no input";

  if (income < 1000) return income * 0.1;
  else if (income < 10000) return income * 0.2;
  else return income * 0.4;
}
