using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Sitecore.Analytics;
using Sitecore.Analytics.Automation;
using Sitecore.Analytics.Automation.Data;
using Sitecore.Analytics.Automation.MarketingAutomation;
using Sitecore.Analytics.Automation.Rules.Workflows;
using Sitecore.Analytics.Data;
using Sitecore.Analytics.Data.Items;
using Sitecore.Analytics.Tracking;
using Sitecore.Diagnostics;
using Sitecore.Shell.Applications.Rules;
using Sitecore.StringExtensions;

namespace Sitecore.Support.Shell.Applications.MarketingAutomation.Dialogs
{
    public class ForceTriggerForm : Sitecore.Shell.Applications.MarketingAutomation.Dialogs.ForceTriggerForm
    {
        protected override bool ActivePageChanging(string page, ref string newpage)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(page, "page");
            Sitecore.Diagnostics.Assert.ArgumentNotNull(newpage, "newpage");
            if (page != null)
            {
                if (!(page == "ContactAndTrigger"))
                {
                    if (page == "Simulation")
                    {
                        if ("Finish".Equals(newpage))
                        {
                            Guid contactId = this.GetContactId(this.ContactName);
                            if (contactId == Guid.Empty)
                            {
                                this.Result.Text = Sitecore.Globalization.Translate.Text("Contact {0} not found.",
                                    new object[]
                                    {
                                        this.ContactName
                                    });
                            }
                            else if (Sitecore.Support.Analytics.Automation.AutomationContactManager.ForceTrigger(contactId, this.TriggerId,
                                 new Sitecore.Data.ID(this.StateId)))
                            {
                                this.Result.Text =
                                    Sitecore.Globalization.Translate.Text("The context has been processed.");
                            }
                            else
                            {
                                this.Result.Text = "Failed to process the visitor. See log for details.";
                            }
                        }
                    }
                }
                else
                {
                    if ("Simulation".Equals(newpage))
                    {
                        if (string.IsNullOrEmpty(this.ContactName))
                        {
                            Sitecore.Web.UI.Sheer.SheerResponse.Alert(
                                Sitecore.Globalization.Translate.Text("You must select a Context."), new string[0]);
                            return false;
                        }
                        if (Sitecore.Data.ID.IsNullOrEmpty(this.TriggerId))
                        {
                            Sitecore.Web.UI.Sheer.SheerResponse.Alert(
                                Sitecore.Globalization.Translate.Text("You must select a trigger."), new string[0]);
                            return false;
                        }
                    }
                    this.SimulateTrigger();
                }
            }
            Assert.ArgumentNotNull(page, "page");
            Assert.ArgumentNotNull(newpage, "newpage");
            return true;
        }

        private new void SimulateTrigger()
        {
            PageEventItem pageEvent = null;
            string pageEventName;
            string text;
            if (this.TriggerId == AutomationUpdater.TimeoutPageEventId)
            {
                pageEventName = "Timeout";
                Sitecore.Data.Items.Item item = this.Master.GetItem(AutomationUpdater.TimeoutPageEventId);
                if (item != null)
                {
                    pageEvent = new PageEventItem(item);
                }
                text = Sitecore.Globalization.Translate.Text("Timeout");
            }
            else
            {
                Sitecore.Data.Items.Item item2 = this.Master.GetItem(this.TriggerId);
                if (item2 == null)
                {
                    return;
                }
                pageEvent = new PageEventItem(item2);
                pageEventName = item2.Name;
                text = item2.DisplayName;
            }
            base.Report = new Sitecore.Web.UI.WebControls.GridPanel();
            base.Report.Attributes["valign"] = "top";
            Sitecore.Web.UI.HtmlControls.Literal child =
                new Sitecore.Web.UI.HtmlControls.Literal(
                    Sitecore.Globalization.Translate.Text("The {2}{0}{3} visitor triggers the {2}{1}{3} event.")
                        .FormatWith(new object[]
                        {
                            this.ContactName,
                            text,
                            "<b>",
                            "</b>"
                        }));
            this.Report.Controls.Add(child);
            this.Report.Controls.Add(new Sitecore.Web.UI.HtmlControls.Space
            {
                Height = Unit.Parse("2px")
            });
            Tracker.Initialize();
            bool isActive = Tracker.IsActive;
            try
            {
                Tracker.IsActive = true;
                ContactRepository contactRepository = new ContactRepository();
                Guid contactId = this.GetContactId(this.ContactName);
                Contact contact = contactRepository.LoadContactReadOnly(contactId);
                if (contact == null)
                {
                    this.ReportBox.InnerHtml = Sitecore.Globalization.Translate.Text("Contact {0} not found.",
                        new object[]
                        {
                            this.ContactName
                        });
                }
                else
                {
                    if (contact.Attachments != null && !contact.Attachments.ContainsKey("KeyBehaviorCache"))
                        PatchReflexionHelper.ContactDelegateInstance(contact);
                    StandardSession standardSession = new StandardSession(contact);
                    using (new SessionSwitcher(standardSession))
                    {
                        standardSession.CreateInteraction(null).GetOrCreateCurrentPage();
                        using (new Sitecore.Security.Accounts.UserSwitcher(this.ContactName, true))
                        {
                            try
                            {
                                IEnumerable<AutomationStateContext> enumerable =
                                    from automationStateContext in contact.AutomationStates().GetAutomationStates()
                                    where automationStateContext.StateId == this.StateId
                                    select automationStateContext;
                                foreach (AutomationStateContext current in enumerable)
                                {
                                    this.ProcessAutomationState(current, pageEventName, pageEvent);
                                }
                                this.ReportBox.InnerHtml = this.Report.RenderAsText();
                            }
                            catch (Exception ex)
                            {
                                this.ReportBox.InnerHtml =
                                    Sitecore.Globalization.Translate.Text("An exception occured while simulating:") +
                                    "<br\\><br\\>" + ex.Message;
                                Sitecore.Diagnostics.Log.Error(ex.Message, ex, this);
                            }
                        }
                    }
                }
            }
            finally
            {
                Tracker.IsActive = isActive;
            }
        }

        private void ProcessAutomationState(AutomationStateContext automationState, string pageEventName,
         PageEventItem pageEvent)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(automationState, "automationState");
            Sitecore.Diagnostics.Assert.ArgumentNotNull(pageEventName, "pageEventName");
            if (pageEventName == "Timeout")
            {
                PatchReflexionHelper.SetPropertyValue(automationState, "IsDue", true);
            }
            Sitecore.Data.Items.Item item = this.Master.GetItem(new Sitecore.Data.ID(automationState.StateId));
            if (item == null)
            {
                return;
            }
            Sitecore.Web.UI.HtmlControls.Border border = new Sitecore.Web.UI.HtmlControls.Border
            {
                Class = "state"
            };
            border.Controls.Add(this.GetNodeIcon("Software/32x32/shape_rectangle.png", "State"));
            border.Controls.Add(new Sitecore.Web.UI.HtmlControls.Literal(this.GetAutomationNodeName(item)));
            this.Report.Controls.Add(border);
            AutomationRuleContext ruleContext = new AutomationRuleContext
            {
                ContactState = automationState,
                StateItem = item,
                Item = Context.Item,
                IsBackgroundUpdater = false,
                PageEventName = pageEventName,
                PageEvent = pageEvent,
                Contact = automationState.AutomationStateManager.Contact
            };
            foreach (Sitecore.Data.Items.Item command in item.Children)
            {
                if (this.ProcessCommand(command, ruleContext))
                {
                    break;
                }
            }
        }

        #region Unchanged
        private Guid GetContactId(string contactName)
        {
            Guid result;
            if (Guid.TryParse(contactName, out result))
            {
                return result;
            }
            if (contactName.StartsWith("Anonymous User\\"))
            {
                string input = contactName.Replace("Anonymous User\\", string.Empty);
                if (Guid.TryParse(input, out result))
                {
                    return result;
                }
            }
            ContactRepository contactRepository = new ContactRepository();
            Contact contact = contactRepository.LoadContactReadOnly(contactName);
            if (contact == null)
            {
                return Guid.Empty;
            }
            return contact.ContactId;
        }

        private bool ProcessCommand(Sitecore.Data.Items.Item command, AutomationRuleContext ruleContext)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(command, "command");
            Sitecore.Diagnostics.Assert.ArgumentNotNull(ruleContext, "ruleContext");
            Sitecore.Web.UI.HtmlControls.Border border = new Sitecore.Web.UI.HtmlControls.Border
            {
                Class = "command"
            };
            Sitecore.Web.UI.HtmlControls.Image nodeIcon = this.GetNodeIcon(command.Appearance.Icon, string.Empty);
            border.Controls.Add(nodeIcon);
            border.Controls.Add(new Sitecore.Web.UI.HtmlControls.Literal(this.GetAutomationNodeName(command)));
            this.Report.Controls.Add(border);
            if (!this.CheckEvaluation(command, ruleContext))
            {
                return false;
            }
            Sitecore.Data.Fields.Field field = command.Fields["Rule"];
            if (field == null)
            {
                return false;
            }
            if (!this.ProcessRules(field, ruleContext))
            {
                return false;
            }
            this.Report.Controls.Add(new Sitecore.Web.UI.HtmlControls.Space
            {
                Height = Unit.Parse("6px")
            });
            this.ShowActions(command);
            this.ShowNextState(command);
            nodeIcon.Src = Sitecore.Resources.Images.GetThemedImageSource("Applications/32x32/check.png");
            return true;
        }

        private void ShowActions(Sitecore.Data.Items.Item command)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(command, "command");
            foreach (Sitecore.Data.Items.Item item in command.Children)
            {
                Sitecore.Web.UI.HtmlControls.Border border = new Sitecore.Web.UI.HtmlControls.Border
                {
                    Class = "action"
                };
                border.Controls.Add(this.GetNodeIcon("Applications/32x32/arrow_right_green.png", "Action"));
                border.Controls.Add(new Sitecore.Web.UI.HtmlControls.Literal(this.GetAutomationNodeName(item)));
                this.Report.Controls.Add(border);
            }
        }

        private void ShowNextState(Sitecore.Data.Items.Item automationCondition)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(automationCondition, "automationCondition");
            Sitecore.Web.UI.HtmlControls.Border border = new Sitecore.Web.UI.HtmlControls.Border
            {
                Class = "action"
            };
            border.Controls.Add(this.GetNodeIcon("Applications/32x32/arrow_right_blue.png", "Action"));
            Sitecore.Data.ID iD = AutomationHelper.FindNextState(automationCondition);
            string text;
            if (iD != Sitecore.Data.ID.Null)
            {
                Sitecore.Data.Items.Item item = this.Master.GetItem(iD);
                text = ((item != null)
                    ? "Move to the {0} state.".FormatWith(new object[]
                    {
                        this.GetAutomationNodeName(item)
                    })
                    : "State {0} is not found.".FormatWith(new object[]
                    {
                        iD
                    }));
            }
            else
            {
                text = "Context remains in this state.";
            }
            border.Controls.Add(new Sitecore.Web.UI.HtmlControls.Literal(text));
            this.Report.Controls.Add(border);
        }

       private Sitecore.Web.UI.HtmlControls.Image GetNodeIcon(string src, string alt)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(src, "src");
            Sitecore.Web.UI.HtmlControls.Image image =
                new Sitecore.Web.UI.HtmlControls.Image(Sitecore.Resources.Images.GetThemedImageSource(src), "16px",
                    "16px");
            image.Style.Add("vertical-align", "text-top");
            image.Margin = "0px 3px 0px 0px";
            if (!string.IsNullOrEmpty(alt))
            {
                image.Alt = alt;
            }
            return image;
        }

        private bool ProcessRules(Sitecore.Data.Fields.Field ruleField, AutomationRuleContext ruleContext)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(ruleField, "ruleField");
            Sitecore.Diagnostics.Assert.ArgumentNotNull(ruleContext, "ruleContext");
            List<string> rulesHtml = this.GetRulesHtml(ruleField.ToString());
            Sitecore.Rules.RuleList<AutomationRuleContext> rules =
                Sitecore.Rules.RuleFactory.GetRules<AutomationRuleContext>(ruleField);
            if (rules == null || rules.Count == 0)
            {
                Sitecore.Web.UI.HtmlControls.Border border = new Sitecore.Web.UI.HtmlControls.Border
                {
                    Class = "rule"
                };
                border.Controls.Add(this.GetNodeIcon("Applications/32x32/bullet_ball_glass_red.png", string.Empty));
                border.Controls.Add(new Sitecore.Web.UI.HtmlControls.Literal("No rules"));
                this.Report.Controls.Add(border);
                return false;
            }
            List<Sitecore.Rules.Rule<AutomationRuleContext>> list =
                rules.Rules.ToList<Sitecore.Rules.Rule<AutomationRuleContext>>();
            using (
                new Sitecore.Diagnostics.LongRunningOperationWatcher(
                    Sitecore.Configuration.Settings.Profiling.RenderFieldThreshold, "Long running rule set: {0}",
                    new string[]
                    {
                        rules.Name ?? string.Empty
                    }))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Sitecore.Rules.Conditions.RuleCondition<AutomationRuleContext> condition = list[i].Condition;
                    if (condition != null)
                    {
                        Sitecore.Rules.RuleStack ruleStack = new Sitecore.Rules.RuleStack();
                        condition.Evaluate(ruleContext, ruleStack);
                        if (ruleContext.IsAborted)
                        {
                            bool result = false;
                            return result;
                        }
                        if (ruleStack.Count != 0)
                        {
                            bool flag = (bool)ruleStack.Pop();
                            Sitecore.Web.UI.HtmlControls.Border border2 = new Sitecore.Web.UI.HtmlControls.Border
                            {
                                Class = "rule"
                            };
                            Sitecore.Web.UI.HtmlControls.Image nodeIcon =
                                this.GetNodeIcon(
                                    flag
                                        ? "Applications/32x32/bullet_ball_glass_green.png"
                                        : "Applications/32x32/bullet_ball_glass_red.png", string.Empty);
                            nodeIcon.Style.Add("float", "left");
                            border2.Controls.Add(nodeIcon);
                            border2.Controls.Add(new Sitecore.Web.UI.HtmlControls.Literal(rulesHtml[i]));
                            this.Report.Controls.Add(border2);
                            if (flag)
                            {
                                bool result = true;
                                return result;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private List<string> GetRulesHtml(string rawRules)
        {
            Assert.ArgumentNotNull(rawRules, "rawRules");
            HtmlTextWriter htmlTextWriter = new HtmlTextWriter(new StringWriter());
            RulesRenderer rulesRenderer = new RulesRenderer(rawRules)
            {
                IsEditable = false
            };
            rulesRenderer.Render(htmlTextWriter);
            rawRules = htmlTextWriter.InnerWriter.ToString();
            rawRules = rawRules.Replace("javascript:return scMouseOver(this,event)", string.Empty);
            rawRules = rawRules.Replace("javascript:return scMouseOut(this,event)", string.Empty);
            rawRules = rawRules.Replace("style=\"color:blue\"", string.Empty);
            string[] separator = new string[]
            {
                "<div class=\"scRule\">"
            };
            return new List<string>(rawRules.Split(separator, StringSplitOptions.RemoveEmptyEntries));
        }

        private bool CheckEvaluation(Sitecore.Data.Items.Item command, AutomationRuleContext ruleContext)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(command, "command");
            string text = "Evaluates: ";
            string a;
            bool flag;
            if ((a = command["Evaluate"]) != null)
            {
                if (a == "{F487AA7E-DF8D-4B8D-B168-07DA8F3CA658}")
                {
                    text += "On Timeout";
                    flag = (ruleContext.PageEventName == "Timeout");
                    goto IL_8B;
                }
                if (a == "{733DCB4D-6477-44DB-87F1-8798B14DB804}")
                {
                    text += "Always Except on Timeout";
                    flag = (ruleContext.PageEventName != "Timeout");
                    goto IL_8B;
                }
            }
            text += "always";
            flag = true;
            IL_8B:
            string str = flag ? "green" : "red";
            Sitecore.Web.UI.HtmlControls.Border border = new Sitecore.Web.UI.HtmlControls.Border
            {
                Class = "rule"
            };
            border.Controls.Add(this.GetNodeIcon("Applications/32x32/bullet_ball_glass_" + str + ".png", string.Empty));
            border.Controls.Add(new Sitecore.Web.UI.HtmlControls.Literal(text));
            this.Report.Controls.Add(border);
            return flag;
        }

        private string GetAutomationNodeName(Sitecore.Data.Items.Item item)
        {
            Sitecore.Diagnostics.Assert.ArgumentNotNull(item, "item");
            return item.DisplayName;
        }

      

        #endregion
    }
}