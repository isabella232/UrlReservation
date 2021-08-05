/*
Copyright (c) 2007, 2015 Austin Wise

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Security.Principal;
using System.Security.AccessControl;

using Austin.HttpApi.Internal;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace Austin.HttpApi
{
    /// <summary>
    /// Represents a reservation for a URL in HTTP.sys.
    /// </summary>
    public class UrlReservation
    {
        private const int GENERIC_EXECUTE = 536870912;

        private readonly string _url;
        private readonly List<SecurityIdentifier> _securityIdentifiers = new List<SecurityIdentifier>();

        /// <summary>
        /// Creates a new reservation object, but does not update HTTP.sys's configuration.
        /// </summary>
        /// <param name="url">The URL pattern of the reservation.</param>
        public UrlReservation(string url)
        {
            _url = url;
        }

        /// <summary>
        /// Creates a new reservation object, but does not update HTTP.sys's configuration.
        /// </summary>
        /// <param name="url">The URL pattern of the reservation.</param>
        /// <param name="securityIdentifiers">The users who have permission to use the reservation.</param>
        public UrlReservation(string url, IList<SecurityIdentifier> securityIdentifiers)
        {
            _url = url;
            _securityIdentifiers.AddRange(securityIdentifiers);
        }
        /// <summary>
        /// The URL pattern of the reservation.
        /// </summary>
        public string Url
        {
            get { return _url; }
        }

        /// <summary>
        /// The names of the users or groups who can make use of the reservation.
        /// </summary>
        public ReadOnlyCollection<string> Users
        {
            get
            {
                List<string> users = new List<string>();
                foreach (SecurityIdentifier sec in _securityIdentifiers)
                {
                    users.Add(((NTAccount)sec.Translate(typeof(NTAccount))).Value);
                }
                return new ReadOnlyCollection<string>(users);
            }
        }

        /// <summary>
        /// The identities of the users who can make use of the reservation.
        /// </summary>
        public ReadOnlyCollection<SecurityIdentifier> SIDs
        {
            get
            {
                return new ReadOnlyCollection<SecurityIdentifier>(_securityIdentifiers);
            }
        }

        /// <summary>
        /// Adds a user to the list of identities who have access to the URL reservation.
        /// </summary>
        /// <param name="user">An NT account name.</param>
        public void AddUser(string user)
        {
            NTAccount account = new NTAccount(user);
            SecurityIdentifier sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
            AddSecurityIdentifier(sid);
        }

        /// <summary>
        /// Adds a user to the list of identities who have access to the URL reservation.
        /// </summary>
        /// <param name="sid">The SID of the user or group.</param>
        public void AddSecurityIdentifier(SecurityIdentifier sid)
        {
            _securityIdentifiers.Add(sid);
        }

        /// <summary>
        /// Clears all entries from the list of security identifiers.
        /// </summary>
        public void ClearUsers()
        {
            this._securityIdentifiers.Clear();
        }

        /// <summary>
        /// Creates the reservation in HTTP.sys.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if a reservation for this URL already exists.</exception>
        /// <exception cref="Win32Exception">Throw if an unexpected error occures while creating the reservation.</exception>
        public void Create()
        {
            UrlReservation.Create(this);
        }

        /// <summary>
        /// Deletes this reservation from the HTTP.sys configuration.
        /// </summary>
        /// <exception cref="Win32Exception">Throw if an unexpected error occures while deleting the reservation.</exception>
        public void Delete()
        {
            UrlReservation.Delete(this);
        }

        #region Get All
        /// <summary>
        /// Returns a list of all configured URL reservations on this computer.
        /// </summary>
        /// <returns></returns>
        public unsafe static ReadOnlyCollection<UrlReservation> GetAll()
        {
            List<UrlReservation> revs = new List<UrlReservation>();

            uint retVal = ErrorCodes.NOERROR;

            retVal = NativeMethods.HttpInitialize(HttpApiConstants.HTTPAPI_VERSION_1, HttpApiConstants.HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
            if (ErrorCodes.NOERROR == retVal)
            {
                try
                {
                    HTTP_SERVICE_CONFIG_URLACL_QUERY inputConfigInfoSet = new HTTP_SERVICE_CONFIG_URLACL_QUERY();
                    inputConfigInfoSet.QueryDesc = HTTP_SERVICE_CONFIG_QUERY_TYPE.HttpServiceConfigQueryNext;
                    inputConfigInfoSet.dwToken = 0;

                    byte[] buf = new byte[128];

                    while (retVal == ErrorCodes.NOERROR)
                    {
                        int returnLength = 0;
                        retVal = NativeMethods.HttpQueryServiceConfiguration(
                            IntPtr.Zero,
                            HTTP_SERVICE_CONFIG_ID.HttpServiceConfigUrlAclInfo,
                            ref inputConfigInfoSet,
                            Marshal.SizeOf(inputConfigInfoSet),
                            null,
                            0,
                            out returnLength,
                            IntPtr.Zero);

                        if (retVal == ErrorCodes.ERROR_INSUFFICIENT_BUFFER && returnLength > buf.Length)
                        {
                            buf = new byte[returnLength];
                        }

                        fixed (byte* pBuf = buf)
                        {
                            retVal = NativeMethods.HttpQueryServiceConfiguration(
                                IntPtr.Zero,
                                HTTP_SERVICE_CONFIG_ID.HttpServiceConfigUrlAclInfo,
                                ref inputConfigInfoSet,
                                Marshal.SizeOf(inputConfigInfoSet),
                                pBuf,
                                buf.Length,
                                out returnLength,
                                IntPtr.Zero);

                            if (retVal == ErrorCodes.NOERROR)
                            {
                                var outputConfigInfo = (HTTP_SERVICE_CONFIG_URLACL_SET)Marshal.PtrToStructure(
                                    new IntPtr(pBuf), typeof(HTTP_SERVICE_CONFIG_URLACL_SET));
                                var rev = new UrlReservation(outputConfigInfo.KeyDesc.pUrlPrefix,
                                    securityIdentifiersFromSDDL(outputConfigInfo.ParamDesc.pStringSecurityDescriptor));
                                revs.Add(rev);
                            }
                        }

                        inputConfigInfoSet.dwToken++;
                    }

                    if (retVal != ErrorCodes.ERROR_NO_MORE_ITEMS)
                    {
                        throw new Win32Exception((int)retVal);
                    }
                }
                finally
                {
                    retVal = NativeMethods.HttpTerminate(HttpApiConstants.HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
                }
            }

            if (ErrorCodes.NOERROR != retVal)
            {
                throw new Win32Exception(Convert.ToInt32(retVal));
            }

            return new ReadOnlyCollection<UrlReservation>(revs);
        }
        #endregion

        #region Create
        static void Create(UrlReservation urlReservation)
        {
            string sddl = generateSddl(urlReservation._securityIdentifiers);
            reserveURL(urlReservation.Url, sddl);
        }

        private static unsafe void reserveURL(string networkURL, string securityDescriptor)
        {
            uint retVal = ErrorCodes.NOERROR;

            retVal = NativeMethods.HttpInitialize(HttpApiConstants.HTTPAPI_VERSION_1, HttpApiConstants.HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
            if (ErrorCodes.NOERROR == retVal)
            {
                try
                {
                    HTTP_SERVICE_CONFIG_URLACL_KEY keyDesc = new HTTP_SERVICE_CONFIG_URLACL_KEY(networkURL);
                    HTTP_SERVICE_CONFIG_URLACL_PARAM paramDesc = new HTTP_SERVICE_CONFIG_URLACL_PARAM(securityDescriptor);

                    HTTP_SERVICE_CONFIG_URLACL_SET inputConfigInfoSet = new HTTP_SERVICE_CONFIG_URLACL_SET();
                    inputConfigInfoSet.KeyDesc = keyDesc;
                    inputConfigInfoSet.ParamDesc = paramDesc;

                    retVal = NativeMethods.HttpSetServiceConfiguration(IntPtr.Zero,
                        HTTP_SERVICE_CONFIG_ID.HttpServiceConfigUrlAclInfo,
                        ref inputConfigInfoSet,
                        Marshal.SizeOf(inputConfigInfoSet),
                        IntPtr.Zero);
                }
                finally
                {
                    NativeMethods.HttpTerminate(HttpApiConstants.HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
                }
            }

            if (ErrorCodes.ERROR_ALREADY_EXISTS == retVal)
            {
                throw new ArgumentException("A reservation for this URL already exists.");
            }
            else if (ErrorCodes.NOERROR != retVal)
            {
                throw new Win32Exception((int)retVal);
            }
        }
        #endregion

        #region Delete
        static void Delete(UrlReservation urlReservation)
        {
            string sddl = generateSddl(urlReservation._securityIdentifiers);
            freeURL(urlReservation.Url, sddl);
        }

        private static void freeURL(string networkURL, string securityDescriptor)
        {
            uint retVal = (uint)ErrorCodes.NOERROR;

            retVal = NativeMethods.HttpInitialize(HttpApiConstants.HTTPAPI_VERSION_1, HttpApiConstants.HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
            if ((uint)ErrorCodes.NOERROR == retVal)
            {
                HTTP_SERVICE_CONFIG_URLACL_KEY urlAclKey = new HTTP_SERVICE_CONFIG_URLACL_KEY(networkURL);
                HTTP_SERVICE_CONFIG_URLACL_PARAM urlAclParam = new HTTP_SERVICE_CONFIG_URLACL_PARAM(securityDescriptor);

                HTTP_SERVICE_CONFIG_URLACL_SET urlAclSet = new HTTP_SERVICE_CONFIG_URLACL_SET();
                urlAclSet.KeyDesc = urlAclKey;
                urlAclSet.ParamDesc = urlAclParam;

                int configInformationSize = Marshal.SizeOf(urlAclSet);

                retVal = NativeMethods.HttpDeleteServiceConfiguration(IntPtr.Zero,
                    HTTP_SERVICE_CONFIG_ID.HttpServiceConfigUrlAclInfo,
                    ref urlAclSet,
                    configInformationSize,
                    IntPtr.Zero);

                NativeMethods.HttpTerminate(HttpApiConstants.HTTP_INITIALIZE_CONFIG, IntPtr.Zero);
            }

            if ((uint)ErrorCodes.NOERROR != retVal)
            {
                throw new Win32Exception(Convert.ToInt32(retVal));
            }
        }
        #endregion

        #region Helper
        private static List<SecurityIdentifier> securityIdentifiersFromSDDL(string securityDescriptor)
        {
            CommonSecurityDescriptor csd = new CommonSecurityDescriptor(false, false, securityDescriptor);
            DiscretionaryAcl dacl = csd.DiscretionaryAcl;

            List<SecurityIdentifier> securityIdentifiers = new List<SecurityIdentifier>(dacl.Count);

            foreach (CommonAce ace in dacl)
            {
                securityIdentifiers.Add(ace.SecurityIdentifier);
            }

            return securityIdentifiers;
        }

        private static DiscretionaryAcl getDacl(List<SecurityIdentifier> securityIdentifiers)
        {
            DiscretionaryAcl dacl = new DiscretionaryAcl(false, false, 16);

            foreach (SecurityIdentifier sec in securityIdentifiers)
            {
                dacl.AddAccess(AccessControlType.Allow, sec, GENERIC_EXECUTE, InheritanceFlags.None, PropagationFlags.None);
            }

            return dacl;
        }

        private static CommonSecurityDescriptor getSecurityDescriptor(List<SecurityIdentifier> securityIdentifiers)
        {
            DiscretionaryAcl dacl = getDacl(securityIdentifiers);

            CommonSecurityDescriptor securityDescriptor =
                new CommonSecurityDescriptor(false, false,
                        ControlFlags.GroupDefaulted |
                        ControlFlags.OwnerDefaulted |
                        ControlFlags.DiscretionaryAclPresent,
                        null, null, null, dacl);
            return securityDescriptor;
        }

        private static string generateSddl(List<SecurityIdentifier> securityIdentifiers)
        {
            return getSecurityDescriptor(securityIdentifiers).GetSddlForm(AccessControlSections.Access);
        }
        #endregion
    }
}
