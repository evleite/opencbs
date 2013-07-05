﻿// Copyright © 2013 Open Octopus Ltd.
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License along
// with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
//
// Website: http://www.opencbs.com
// Contact: contact@opencbs.com

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using OpenCBS.CoreDomain;
using OpenCBS.CoreDomain.Contracts;
using OpenCBS.GUI.UserControl;
using OpenCBS.Services;

namespace OpenCBS.GUI.Contracts
{
    public partial class ReassignContractsForm : SweetForm
    {
        private List<User> _users;
        private IEnumerable<ReassignContractItem> _contracts;
        private string _filter;

        public ReassignContractsForm()
        {
            InitializeComponent();
            Setup();
        }

        private void Setup()
        {
            Load += (sender, args) => LoadForm();
            fromCombobox.SelectedIndexChanged += (sender, args) => ReloadContracts();
            filterTextbox.TextChanged += (sender, args) =>
            {
                _filter = filterTextbox.Text;
                ShowContracts();
            };
            contractsObjectListView.ItemChecked += (sender, args) => UpdateTitle();
            assignButton.Click += (sender, args) => Reassign();
            selectAllCheckbox.CheckedChanged += (sender, args) => SelectAll();
        }

        private void LoadForm()
        {
            LoadUsers();
            fromCombobox.SelectedIndex = 0;
            toCombobox.SelectedIndex = 0;
            olbColumn.AspectToStringConverter =
            amountColumn.AspectToStringConverter = value =>
            {
                var amount = (decimal)value;
                return amount.ToString("N2");
            };
            startDateColumn.AspectToStringConverter =
            closeDateColumn.AspectToStringConverter = value =>
            {
                var date = (DateTime)value;
                return date.ToShortDateString();
            };
            interestRateColumn.AspectToStringConverter = value =>
            {
                var interestRate = (decimal)value;
                return (interestRate*100).ToString("N2") + " %";
            };
        }

        private void LoadUsers()
        {
            fromCombobox.Items.Clear();

            _users = ServicesProvider.GetInstance().GetUserServices().FindAll(false).OrderBy(item => item.LastName).ThenBy(item => item.FirstName).ToList();

            foreach (var user in _users)
            {
                fromCombobox.Items.Add(user);

                if (!user.IsDeleted && (user.UserRole.IsRoleForLoan || user.UserRole.IsRoleForSaving))
                {
                    toCombobox.Items.Add(user);
                }
            }
        }

        private void Reassign()
        {
            var from = fromCombobox.SelectedItem as User;
            var to = toCombobox.SelectedItem as User;
            if (from == null || to == null) return;
            if (from.Id == to.Id) return;

            var selectedContracts =
                from contract in contractsObjectListView.Objects as IEnumerable<ReassignContractItem>
                where contract.CanReassign
                select contract;
            if (!selectedContracts.Any()) return;

            if (!Confirm("Do you confirm the operation?")) return;

            var cursor = Cursor;
            var backgroundWorkder = new BackgroundWorker();
            backgroundWorkder.DoWork += (sender, args) =>
            {
                var loanService = ServicesProvider.GetInstance().GetContractServices();
                foreach (var contract in selectedContracts)
                {
                    loanService.ReassignContract(contract.ContractId, to.Id, from.Id);
                }
            };
            backgroundWorkder.RunWorkerCompleted += (sender, args) =>
            {
                Enable();
                Cursor = cursor;
                if (args.Error != null)
                {
                    Fail(args.Error.Message);
                    return;
                }
                ReloadContracts();
            };
            Cursor = Cursors.WaitCursor;
            Disable();
            backgroundWorkder.RunWorkerAsync();
        }

        private void Disable()
        {
            optionsPanel.Enabled = false;
        }

        private void Enable()
        {
            optionsPanel.Enabled = true;
        }

        private void ShowContracts()
        {
            if (string.IsNullOrEmpty(_filter))
            {
                contractsObjectListView.SetObjects(_contracts);
                UpdateTitle();
                return;
            }

            var filteredContracts = from contract in _contracts
                                    where contract.ClientLastName.ToLower().Contains(_filter.ToLower())
                                    select contract;
            contractsObjectListView.SetObjects(filteredContracts);
            UpdateTitle();
        }

        private void ReloadContracts()
        {
            var user = fromCombobox.SelectedItem as User;
            if (user == null) return;

            var cursor = Cursor;
            var backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += (sender, args) =>
            {
                var loanService = ServicesProvider.GetInstance().GetContractServices();
                _contracts = loanService.FindContractsByLoanOfficerId(user.Id, false);
            };
            backgroundWorker.RunWorkerCompleted += (sender, args) =>
            {
                Enable();
                Cursor = cursor;
                if (args.Error != null)
                {
                    Fail(args.Error.Message);
                    return;
                }
                ShowContracts();
            };
            Cursor = Cursors.WaitCursor;
            Disable();
            _contracts = new List<ReassignContractItem>();
            ShowContracts();
            backgroundWorker.RunWorkerAsync();
        }

        private void UpdateTitle()
        {
            var contracts = contractsObjectListView.Objects as IEnumerable<ReassignContractItem>;
            if (contracts == null || !contracts.Any())
            {
                Text = "Reassign contracts";
                return;
            }
            var number = contracts.Count();
            var numberOfChecked = (from contract in contracts
                                   where contract.CanReassign
                                   select contract).Count();
            Text = string.Format("Reassign contracts ({0} of {1})", numberOfChecked, number);
        }

        private void SelectAll()
        {
            var contracts = contractsObjectListView.Objects as IEnumerable<ReassignContractItem>;
            if (contracts == null) return;
            foreach (var contract in contracts)
            {
                contract.CanReassign = selectAllCheckbox.Checked;
                contractsObjectListView.RefreshObject(contract);
            }
        }
    }
}
