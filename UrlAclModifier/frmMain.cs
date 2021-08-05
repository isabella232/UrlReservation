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
using System.Collections.ObjectModel;

namespace Austin.UrlAclModifier
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            refreshReservations();
            WindowsPrincipal p = new WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent());
            lblAdminWarning.Visible = !p.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            refreshReservations();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmAbout frm = new frmAbout();
            frm.ShowDialog();
            frm.Dispose();
        }

        private void addNew_Click(object sender, EventArgs e)
        {
            frmAddReservation frm = new frmAddReservation();
            if (frm.ShowDialog() == DialogResult.OK)
                refreshReservations();
            frm.Dispose();
        }

        private void delete_Click(object sender, EventArgs e)
        {
            UrlReservation rev = (UrlReservation)reservationSource.Current;
            if (MessageBox.Show(string.Format("Are you sure you want to delete the reservation of {0} for {1}?", rev.Url, getNameOfUsers(rev.Users)), Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                rev.Delete();
                refreshReservations();
            }
        }

        private string getNameOfUsers(IList<string> users)
        {
            switch (users.Count)
            {
                case 0:
                    return string.Empty;
                case 1:
                    return users[0];
                case 2:
                    return string.Format("{0} and {1}", users[0], users[1]);
                default:
                    string ret = string.Empty;
                    for (int i = 0; i < users.Count - 2; i++)
                    {
                        ret += string.Format("{0}, ", users[i]);
                    }
                    ret += string.Format(" and {0}", users[users.Count - 1]);
                    return ret;
            }
        }

        private void refreshReservations()
        {
            reservationSource.DataSource = UrlReservation.GetAll();
        }

        private void reservations_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            object value = reservations.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            if (value.GetType() == typeof(ReadOnlyCollection<string>))
            {
                ReadOnlyCollection<string> users = (ReadOnlyCollection<string>)value;
                e.Value = getNameOfUsers(users);
            }
        }
    }
}