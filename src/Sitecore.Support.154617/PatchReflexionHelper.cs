using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using Sitecore.Analytics.Tracking;

namespace Sitecore.Support
{
    public class PatchReflexionHelper
    {
        private static MethodInfo _method = typeof(Sitecore.Analytics.Tracking.ContactKeyBehaviorCacheExtension).GetMethod("ResetKeyBehaviorCache", BindingFlags.NonPublic | BindingFlags.Static);

        public delegate void ContactDelegate(Contact contact);
        
        public static ContactDelegate ContactDelegateInstance = (ContactDelegate)Delegate.CreateDelegate(typeof(ContactDelegate), _method);


        public static void SetPropertyValue(object obj, string propertyName, object val)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            Type objType = obj.GetType();
            PropertyInfo propInfo = GetPropertyInfo(objType, propertyName);
            if (propInfo == null)
                throw new ArgumentOutOfRangeException("propertyName",
                    string.Format("Couldn't find property {0} in type {1}", propertyName, objType.FullName));
            propInfo.SetValue(obj, val, null);
        }

        private static PropertyInfo GetPropertyInfo(Type type, string propertyName)
        {
            PropertyInfo propInfo = null;
            do
            {
                propInfo = type.GetProperty(propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                type = type.BaseType;
            } while (propInfo == null && type != null);
            return propInfo;
        }

    }
}