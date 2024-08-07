//#execOnChange
//buddy development stuff
if(isFunction("execChangedAddFile")) execChangedAddFile(ExpandFilename("./client.cs"));

exec("./farmingGui.cs");

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
		switch$(firstWord(%message))
		{
			case "/qfHelp" or "/fbHelp":
				newChatHud_AddLine("\c6Commands:");
				newChatHud_AddLine("\c3  /qfToggle");
				newChatHud_AddLine("\c3  /qfName \c7[\c6ID\c7] \c7[\c6Name\c7]");

			case "/qfToggle" or "/fbToggle":
				$QualityFarming::Enabled = !$QualityFarming::Enabled;
				newChatHud_AddLine("\c6QualityFarming is now" SPC ($QualityFarming::Enabled ? "\c3Enabled" : "\c0Disabled"));

			case "/qfList" or "/fbList":
				QualityFarming_listBoundIDs(restWords(%message));

			case "/qfName" or "/fbName":
				QualityFarming_setIDName(restWords(%message));
		}

		return parent::send(%this);
	}

	function clientCmdCenterprint(%message, %time)
	{
		if(!$QualityFarming::Enabled)
			return parent::clientCmdCenterprint(%message, %time);

		%message = QualityFarming_parseToolIDString(%message);
		
		parent::clientCmdCenterprint(%message, %time);
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

function QualityFarming_parseToolIDString(%string)
{
	%lines = getRecordCount(%string);
	for(%i = 0; %i < %lines; %i++)
	{
		%line = getRecord(%string, %i);
		%pos0 = stripos(%line, "[");
		%pos1 = stripos(%line, "]");
		if(%pos0 == -1 || %pos1 == -1 || %pos1 - %pos0 != 4)
			continue;
		
		%toolID = getSubStr(%line, %pos0 + 1, 3);
		if(!QualityFarming_veryfiyID(%toolID))
			continue;
		
		%name = QualityFarming_getToolName(%toolID);
		if(%name !$= "")
		{
			%newLine = getSubStr(%line, 0, %pos0 + 1) @ %name @ getSubStr(%line, %pos1, 256);
			%string = setRecord(%string, %i, %newLine);
		} else {
			$QualityFarming::lastSeenToolID = %toolID;
		}
	}

	return %string;
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

	%name = restWords(%data);
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

function QualityFarming_listBoundIDs(%search)
{
	newChatHud_AddLine("\c6Total Tool Data Records #" @ $QualityFarming::ToolDataCount + 0);
	for(%i = 0; %i < $QualityFarming::ToolDataCount; %i++)
	{
		%toolID = $QualityFarming::ToolDataIndex[%i];

		newChatHud_AddLine("  \c3["@ %toolID @"]:\t\c6 "@ $QualityFarming::ToolData[%toolID, "name"]);
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


