/*
Copyright (c) 2007 Austin Wise

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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using Austin.HttpApi;

using System.Security.Principal;

namespace Austin.UrlAclModifier
{
    public partial class frmAddReservation : Form
    {
        public frmAddReservation()
        {
            InitializeComponent();
        }

        private List<string> users = new List<string>();
        private List<SecurityIdentifier> sids = new List<SecurityIdentifier>();

        private void btnCreate_Click(object sender, EventArgs e)
        {
            if (sids.Count == 0)
            {
                MessageBox.Show("You must add at least on user.");
                txtUserName.Focus();
                return;
            }
            try
            {
                UrlReservation rev = new UrlReservation(this.txtUrl.Text);
                foreach (SecurityIdentifier sid in sids)
                {
                    rev.AddSecurityIdentifier(sid);
                }
                rev.Create();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Failed to create reservation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnAddUser_Click(object sender, EventArgs e)
        {
            NTAccount act;
            SecurityIdentifier sid;
            try
            {
                act = new NTAccount(txtUserName.Text);
                //Translate the account to make sure its a real user
                sid = (SecurityIdentifier)act.Translate(typeof(SecurityIdentifier));
            }
            catch
            {
                err.SetError(txtUserName, "Invalid user name.");
                return;
            }

            if (this.sids.Contains(sid))
            {
                err.SetError(txtUserName, "User is already in the list.");
                return;
            }

            this.sids.Add(sid);
            this.users.Add(((NTAccount)sid.Translate(typeof(NTAccount))).Value);

            err.Clear();
            txtUserName.Clear();

            this.lbUsers.DataSource = null;
            this.lbUsers.DataSource = this.users;
        }
    }
}