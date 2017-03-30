using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.Analytics;
using Sitecore.Analytics.Automation;
using Sitecore.Analytics.Automation.Data;
using Sitecore.Analytics.Automation.MarketingAutomation;
using Sitecore.Analytics.Data;
using Sitecore.Analytics.Data.Items;
using Sitecore.Analytics.DataAccess;
using Sitecore.Analytics.Model;
using Sitecore.Analytics.Tracking;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Security.Accounts;

namespace Sitecore.Support.Analytics.Automation
{
    public class AutomationContactManager: Sitecore.Analytics.Automation.AutomationContactManager
    {
        public new static bool ForceTrigger(Guid contactId, ID pageEventId, ID stateId)
        {
            Assert.ArgumentNotNull(pageEventId, "pageEventId");
            Assert.ArgumentCondition(contactId != Guid.Empty, "contactId", "contactId cannot be empty.");
            Assert.ArgumentNotNull(stateId, "stateId");
            Tracker.Initialize();
            bool isActive = Tracker.IsActive;
            bool result;
            try
            {
                Tracker.IsActive = true;
                ContactRepository contactRepository = new ContactRepository();
                string ownerIdentifier = Guid.NewGuid().ToString();
                LeaseOwner leaseOwner = new LeaseOwner(ownerIdentifier, LeaseOwnerType.OutOfRequestWorker);
                LockAttemptResult<Contact> lockAttemptResult = contactRepository.TryLoadContact(contactId, leaseOwner,
                    TimeSpan.FromSeconds(10.0));
                if (lockAttemptResult.Status == LockAttemptStatus.Success && lockAttemptResult.Object != null)
                {
                    try
                    {
                        Contact @object = lockAttemptResult.Object;
                        if (@object.Attachments != null && !@object.Attachments.ContainsKey("KeyBehaviorCache"))
                            PatchReflexionHelper.ContactDelegateInstance(@object);
                        AutomationStateContext[] array =
                        (from automationStateContext in @object.AutomationStates().GetAutomationStates()
                         where automationStateContext.StateId == stateId.Guid
                         select automationStateContext).ToArray<AutomationStateContext>();
                        PageEventItem pageEvent = null;
                        string pageEventName;
                        if (pageEventId == AutomationUpdater.TimeoutPageEventId)
                        {
                            pageEventName = string.Empty;
                            Database definitionDatabase = Tracker.DefinitionDatabase;
                            Item item = definitionDatabase.GetItem(pageEventId);
                            if (item != null)
                            {
                                pageEvent = new PageEventItem(item);
                            }
                            AutomationStateContext[] array2 = array;
                            for (int i = 0; i < array2.Length; i++)
                            {
                                AutomationStateContext automationStateContext2 = array2[i];
                                PatchReflexionHelper.SetPropertyValue(automationStateContext2, "IsDue", true);
                            }
                        }
                        else
                        {
                            Database definitionDatabase2 = Tracker.DefinitionDatabase;
                            Item item2 = definitionDatabase2.GetItem(pageEventId);
                            if (item2 == null)
                            {
                                Log.Error("Could not find page event: " + pageEventId, typeof(AutomationContactManager));
                                result = false;
                                return result;
                            }
                            pageEventName = item2.Name;
                            pageEvent = new PageEventItem(item2);
                        }
                        StandardSession standardSession = new StandardSession(@object);

                        using (new SessionSwitcher(standardSession))
                        {

                            CurrentInteraction currentInteraction = standardSession.CreateInteraction(null);
                            currentInteraction.GetOrCreateCurrentPage();
                            string text = @object.Identifiers.Identifier;
                            text = (string.IsNullOrWhiteSpace(text) ? ("Anonymous User\\" + @object.ContactId) : text);
                            using (new UserSwitcher(text, true))
                            {
                                try
                                {
                                    AutomationStateContext[] array3 = array;
                                    for (int j = 0; j < array3.Length; j++)
                                    {
                                        AutomationStateContext stateContext = array3[j];
                                        AutomationUpdater.Process(stateContext, currentInteraction, pageEventName,
                                            pageEvent);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex.Message, ex, typeof(AutomationContactManager));
                                    result = false;
                                    return result;
                                }
                            }
                        }
                        contactRepository.SaveContact(@object, new ContactSaveOptions(true, leaseOwner, null));
                        result = true;
                        return result;
                    }
                    catch (Exception exception)
                    {
                        Log.Error("Error occured while trying to force triggers.", exception,
                            typeof(AutomationContactManager));
                        contactRepository.ReleaseContact(contactId, leaseOwner);
                        result = false;
                        return result;
                    }
                }
                Log.Error(
                    string.Format("Cannot obtain lock on contact: {0}. Status: {1}", contactId, lockAttemptResult.Status),
                    typeof(AutomationContactManager));
                result = false;
            }
            finally
            {
                Tracker.IsActive = isActive;
            }
            return result;
        }
    }
}