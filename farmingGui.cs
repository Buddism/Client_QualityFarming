$FarmingPrintActive = 0;
function clientCmdFarmingPrint (%message, %time, %size)
{
	if ($FarmingPrintActive)
	{
		if ($FarmingPrintDlg::removePrintEvent != 0)
		{
			cancel ($FarmingPrintDlg::removePrintEvent);
			$FarmingPrintDlg::removePrintEvent = 0;
		}
	}
	else 
	{
		FarmingPrintDlg.visible = 1;
		$FarmingPrintActive = 1;
	}
	FarmingPrintText.setText ("<just:center>" @ %message @ "\n");
	if (%time > 0)
	{
		$FarmingPrintDlg::removePrintEvent = schedule (%time * 1000, 0, "clientCmdClearFarmingPrint");
	}
}

function clientCmdClearFarmingPrint ()
{
	$FarmingPrintActive = 0;
	FarmingPrintDlg.visible = 0;
	if (isEventPending ($FarmingPrintDlg::removePrintEvent))
	{
		cancel ($FarmingPrintDlg::removePrintEvent);
	}
	$FarmingPrintDlg::removePrintEvent = 0;
}
if(!isObject(FarmingPrintDlg))
	new GuiSwatchCtrl(FarmingPrintDlg : CenterPrintDlg) { new GuiMLTextCtrl(FarmingPrintText : CenterPrintText); };
PlayGui.add(FarmingPrintDlg);

function FarmingPrintText::onResize (%this, %width, %height)
{
	CenterPrintText::onResize (%this, %width, %height);
}