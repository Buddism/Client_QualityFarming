//#execOnChange
//buddy development stuff
if(isFunction("execChangedAddFile")) execChangedAddFile(ExpandFilename("./client.cs"));

if($Pref::QualityFarming::SavePath $= "")
	$Pref::QualityFarming::SavePath = "config/client/QualityFarming.cs";

if(isFile($Pref::QualityFarming::SavePath))
{
	if($QualityFarming::Loaded)
	{
		exec($Pref::QualityFarming::SavePath);
	} else {
		$QualityFarming::DateCreated = getDateTime();
	}

	$QualityFarming::Loaded = true;
	if(!isEventPending($QualityFarmingAutoSave))
		QualityFarming_save();
}

package QualityFarming
{
	function NMH_Type::send(%this)
	{
		%message = %this.getValue();
		if(getSubStr(%message, 0, 1) !$= "/")
			return parent::send(%this);
		
		// /qf and /fb cuz !fb is used habitually
		%commandArgs = restWords(%message);
		switch$(firstWord(%message))
		{
			case "/qfHelp" or "/fbHelp":
				newChatHud_AddLine("\c6Commands:");
				newChatHud_AddLine("\c3  /qfToggle");
				newChatHud_AddLine("\c3  /qfName \c7[\c6ID\c7] \c7[\c6Name\c7]");
				newChatHud_AddLine("\c3  \c0/qfDelete \c7[\c6ID\c7] - delete all tool info for this id");

			case "/qfToggle" or "/fbToggle":
				$QualityFarming::Enabled = !$QualityFarming::Enabled;
				newChatHud_AddLine("\c6QualityFarming is now" SPC ($QualityFarming::Enabled ? "\c3Enabled" : "\c0Disabled"));

			case "/qfList" or "/fbList":
				QualityFarming_listBoundIDs(%commandArgs);

			case "/qfName" or "/fbName":
				QualityFarming_setIDName(%commandArgs);

			case "/qfDelete" or "/fbDelete":
				%didDeletion = QualityFarming_DeleteToolID(%commandArgs);
				if(%didDeletion)
				{
					newChatHud_AddLine("\c6QualityFarming: \c6Deleted tool tata!");
				} else {
					newChatHud_AddLine("\c6QualityFarming: \c0Failed to find tool id!");
				}
		}

		return parent::send(%this);
	}

	function clientCmdCenterprint(%message, %time)
	{
		if(!$QualityFarming::Enabled)
			return parent::clientCmdCenterprint(%message, %time);

		%lines = getRecordCount(%message);
		for(%i = 0; %i < %lines; %i++)
		{
			%line = getRecord(%message, %i);
			%line = QualityFarming_parseToolIDString(%line);

			%newMessage = %i == 0 ? %line : %newMessage NL %line;
		}
		
		parent::clientCmdCenterprint(%newMessage, %time);
	}

	function clientCmdMessageBoxOK (%title, %message)
	{
		if($QualityFarming::Enabled
			&& $QualityFarming::PreventStorageFullBox
			&& deTag(%title)   $= "Storage Full"
			&& deTag(%message) $= "Cannot insert this item!")
			return;

		return parent::clientCmdMessageBoxOK (%title, %message);
	}
};
activatePackage(QualityFarming);

function QualityFarming_parseToolIDString(%line)
{
	//check if there is an item id here
	%pos0 = stripos(%line, "[");
	%pos1 = stripos(%line, "]");
	if(%pos0 == -1 || %pos1 == -1 || %pos1 - %pos0 != 4)
		return %line;
	
	%toolID = getSubStr(%line, %pos0 + 1, 3);
	if(!QualityFarming_veryfiyID(%toolID))
		return %line;
	
	%name = QualityFarming_getToolName(%toolID);
	if(%name !$= "")
	{
		%newLine = getSubStr(%line, 0, %pos0 + 1) @ %name @ getSubStr(%line, %pos1, 256);
		return %newLine;
	} else {
		$QualityFarming::lastSeenToolID = %toolID;
	}

	return %line;
}

function QualityFarming_veryfiyID(%id)
{
	if(strlen(%id) != 3)
		return false;

	if(strlen(stripChars(strlwr(%id), "abcdefghijklmnopqrstuvwxyz" @ "0123456789")) != 0)
		return false;
	
	return true;
}

function QualityFarming_getToolName(%toolID)
{
	return $QualityFarming::ToolData[%toolID, "name"];
}

function QualityFarming_setIDName(%data)
{
	%toolID = getWord(%data, 0);
	if(%toolID $= "last")
	{
		%toolID = $QualityFarming::lastSeenToolID;
	}

	%name = collapseEscape(restWords(%data));
	if(!QualityFarming_veryfiyID(%toolID))
	{
		newChatHud_AddLine("\c6QualityFarming: Error id is invalid");
		return;
	}
	
	%index = QualityFarming_getToolIndex(%toolID);
	if(%index == -1)
		QualityFarming_addToolIndex(%toolID);

	$QualityFarming::ToolData[%toolID, "name"] = %name;
	newChatHud_AddLine("\c6QualityFarming: Named ["@ %toolID @"] to '"@ %name @"'");
}
function QualityFarming_getToolIndex(%testToolID)
{
	for(%i = 0; %i < $QualityFarming::ToolDataCount; %i++)
	{
		%toolID = $QualityFarming::ToolDataIndex[%i];
		if(%testToolID $= %toolID)
			return %i;
	}

	return -1;
}

function QualityFarming_addToolIndex(%toolID)
{
	if(QualityFarming_getToolIndex(%toolID) != -1)
		return false;
	
	$QualityFarming::ToolDataExists[%toolID] = true;

	$QualityFarming::ToolDataIndex[$QualityFarming::ToolDataCount + 0] = %toolID;
	$QualityFarming::ToolDataCount++;	
}

function QualityFarming_deleteToolID(%toolID)
{
	%toolIndex = QualityFarming_getToolIndex(%toolID);
	if(%toolIndex == -1)
		return false;

	for(%i = %toolIndex; %i < $QualityFarming::ToolDataCount; %i++)
	{
		$QualityFarming::ToolDataIndex[%i] = $QualityFarming::ToolDataIndex[%i + 1];
	}

	$QualityFarming::ToolDataCount--;
	$QualityFarming::ToolDataIndex[$QualityFarming::ToolDataCount] = "";

	deleteVariables("$QualityFarming::ToolData" @ %toolID @"_*");
	return true;
}

function QualityFarming_listBoundIDs(%search)
{
	newChatHud_AddLine("\c6Total Tool Data Records #" @ $QualityFarming::ToolDataCount + 0);
	for(%i = 0; %i < $QualityFarming::ToolDataCount; %i++)
	{
		%toolID = $QualityFarming::ToolDataIndex[%i];

		newChatHud_AddLine("  <spush><font:courier new:20>\c3["@ %toolID @"]<spop>\c6 "@ $QualityFarming::ToolData[%toolID, "name"]);
	}
}

function QualityFarming_save()
{
	$QualityFarming::lastSaveTime = getDateTime();
	export("$QualityFarming::*", $Pref::QualityFarming::SavePath);

	if(!isObject(serverConnection))
		return;
	
	cancel($QualityFarmingAutoSave);
	$QualityFarmingAutoSave = schedule(30000, 0, QualityFarming_save);
}


